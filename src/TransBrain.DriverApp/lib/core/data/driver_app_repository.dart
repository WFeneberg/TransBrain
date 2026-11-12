import '../models/driver.dart';
import '../models/tour.dart';
import '../models/tour_route.dart';

/// Everything the driver app reads and reports. Implemented against the real
/// API and against bundled demo data.
abstract class DriverAppRepository {
  /// The caller's tours. The API scopes a fahrer-only token to their own
  /// tours server-side, so no client-side filter is needed.
  Future<List<Tour>> listMyTours();

  Future<Tour> getTour(String id);

  Future<TourRoute> getRoute(String id);

  Future<Tour> startTour(String id);

  Future<Tour> completeTour(String id);

  /// The driver record linked to the signed-in user, or null when none is.
  Future<Driver?> findMyDriver(String? externalUserId);
}
