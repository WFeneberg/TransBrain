import '../api/api_client.dart';
import '../models/driver.dart';
import '../models/paged_result.dart';
import '../models/tour.dart';
import '../models/tour_route.dart';
import 'driver_app_repository.dart';

/// The real thing: talks to the TransBrain API with the signed-in user's
/// token.
class HttpDriverAppRepository implements DriverAppRepository {
  HttpDriverAppRepository(this._api);

  final ApiClient _api;

  @override
  Future<List<Tour>> listMyTours() async {
    final List<Tour> tours = <Tour>[];
    int page = 1;
    PagedResult<Tour> result;
    do {
      result = PagedResult<Tour>.fromJson(
        await _api.getJson('/api/tours', <String, String>{
          'page': '$page',
          'pageSize': '50',
        }) as Map<String, dynamic>,
        Tour.fromJson,
      );
      tours.addAll(result.items);
      page++;
      // A driver has a handful of tours; cap defensively anyway.
    } while (result.hasMore && page <= 10);
    return tours;
  }

  @override
  Future<Tour> getTour(String id) async => Tour.fromJson(
      await _api.getJson('/api/tours/$id') as Map<String, dynamic>);

  @override
  Future<TourRoute> getRoute(String id) async => TourRoute.fromJson(
      await _api.getJson('/api/tours/$id/route') as Map<String, dynamic>);

  @override
  Future<Tour> startTour(String id) async => Tour.fromJson(
      await _api.postJson('/api/tours/$id/start') as Map<String, dynamic>);

  @override
  Future<Tour> completeTour(String id) async => Tour.fromJson(
      await _api.postJson('/api/tours/$id/complete') as Map<String, dynamic>);

  @override
  Future<Driver?> findMyDriver(String? externalUserId) async {
    if (externalUserId == null || externalUserId.isEmpty) {
      return null;
    }
    int page = 1;
    PagedResult<Driver> result;
    do {
      result = PagedResult<Driver>.fromJson(
        await _api.getJson('/api/drivers', <String, String>{
          'page': '$page',
          'pageSize': '50',
        }) as Map<String, dynamic>,
        Driver.fromJson,
      );
      for (final Driver driver in result.items) {
        if (driver.externalUserId == externalUserId) {
          return driver;
        }
      }
      page++;
    } while (result.hasMore && page <= 10);
    return null;
  }
}
