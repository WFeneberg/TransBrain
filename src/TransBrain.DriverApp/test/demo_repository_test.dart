import 'package:flutter_test/flutter_test.dart';
import 'package:transbrain_driver/core/api/api_exception.dart';
import 'package:transbrain_driver/core/data/demo_driver_app_repository.dart';
import 'package:transbrain_driver/core/models/driver.dart';
import 'package:transbrain_driver/core/models/tour.dart';
import 'package:transbrain_driver/core/models/tour_route.dart';

void main() {
  group('DemoDriverAppRepository', () {
    test('listMyTours_freshRepository_returnsThreeToursInAllStates', () async {
      final DemoDriverAppRepository repo = DemoDriverAppRepository();
      final List<Tour> tours = await repo.listMyTours();
      expect(tours, hasLength(3));
      expect(tours.map((Tour t) => t.status).toSet(),
          <String>{'Planned', 'InProgress', 'Completed'});
    });

    test('startTour_plannedTour_becomesInProgress', () async {
      final DemoDriverAppRepository repo = DemoDriverAppRepository();
      final Tour started = await repo.startTour('demo-tour-1');
      expect(started.isInProgress, isTrue);
      expect((await repo.getTour('demo-tour-1')).isInProgress, isTrue);
    });

    test('startTour_completedTour_throwsConflict', () async {
      final DemoDriverAppRepository repo = DemoDriverAppRepository();
      await expectLater(
        repo.startTour('demo-tour-3'),
        throwsA(isA<ApiException>()
            .having((ApiException e) => e.statusCode, 'statusCode', 409)),
      );
    });

    test('completeTour_inProgressTour_becomesCompleted', () async {
      final DemoDriverAppRepository repo = DemoDriverAppRepository();
      final Tour completed = await repo.completeTour('demo-tour-2');
      expect(completed.isCompleted, isTrue);
    });

    test('completeTour_plannedTour_throwsConflict', () async {
      final DemoDriverAppRepository repo = DemoDriverAppRepository();
      await expectLater(
        repo.completeTour('demo-tour-1'),
        throwsA(isA<ApiException>()
            .having((ApiException e) => e.statusCode, 'statusCode', 409)),
      );
    });

    test('getTour_unknownId_throwsNotFound', () async {
      final DemoDriverAppRepository repo = DemoDriverAppRepository();
      await expectLater(
        repo.getTour('nope'),
        throwsA(isA<ApiException>()
            .having((ApiException e) => e.statusCode, 'statusCode', 404)),
      );
    });

    test('getRoute_everyTour_hasStopsGeometryAndSchedule', () async {
      final DemoDriverAppRepository repo = DemoDriverAppRepository();
      for (final Tour tour in await repo.listMyTours()) {
        final TourRoute route = await repo.getRoute(tour.id);
        expect(route.stops, isNotEmpty, reason: tour.id);
        expect(route.geometry.length, greaterThanOrEqualTo(2), reason: tour.id);
        expect(route.schedule, isNotEmpty, reason: tour.id);
        expect(route.end.isAfter(route.departure), isTrue, reason: tour.id);
      }
    });

    test('findMyDriver_anyUser_returnsDemoDriver', () async {
      final DemoDriverAppRepository repo = DemoDriverAppRepository();
      final Driver? driver = await repo.findMyDriver('demo-user');
      expect(driver, isNotNull);
      expect(driver!.fullName, 'Max Mustermann');
      expect(driver.licenseClasses, containsAll(<String>['C', 'CE']));
    });
  });
}
