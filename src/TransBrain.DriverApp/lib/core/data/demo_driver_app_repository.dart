import '../api/api_exception.dart';
import '../models/driver.dart';
import '../models/tour.dart';
import '../models/tour_route.dart';
import 'driver_app_repository.dart';

/// Bundled demo data: lets anyone try the app — including the web demo —
/// without Keycloak, API or database. State lives in memory, so starting and
/// completing tours behaves like the real thing until the app restarts.
class DemoDriverAppRepository implements DriverAppRepository {
  DemoDriverAppRepository() {
    final DateTime today = DateTime.now();
    final DateTime date = DateTime(today.year, today.month, today.day);
    _tours = <String, Tour>{
      'demo-tour-1': _tour(
        id: 'demo-tour-1',
        date: date,
        plate: 'LL-TB 101',
        status: 'Planned',
        weightKg: 14800,
        loadMeters: 9.2,
        stops: const <(String, String, String)>[
          ('TB-2025-0142', 'Pickup', 'o-142'),
          ('TB-2025-0142', 'Delivery', 'o-142'),
          ('TB-2025-0147', 'Pickup', 'o-147'),
          ('TB-2025-0147', 'Delivery', 'o-147'),
        ],
      ),
      'demo-tour-2': _tour(
        id: 'demo-tour-2',
        date: date,
        plate: 'LL-TB 204',
        status: 'InProgress',
        weightKg: 8200,
        loadMeters: 4.8,
        stops: const <(String, String, String)>[
          ('TB-2025-0150', 'Pickup', 'o-150'),
          ('TB-2025-0150', 'Delivery', 'o-150'),
        ],
      ),
      'demo-tour-3': _tour(
        id: 'demo-tour-3',
        date: date.subtract(const Duration(days: 1)),
        plate: 'LL-TB 101',
        status: 'Completed',
        weightKg: 19600,
        loadMeters: 12.4,
        stops: const <(String, String, String)>[
          ('TB-2025-0128', 'Pickup', 'o-128'),
          ('TB-2025-0131', 'Pickup', 'o-131'),
          ('TB-2025-0128', 'Delivery', 'o-128'),
          ('TB-2025-0131', 'Delivery', 'o-131'),
        ],
      ),
    };
  }

  static const Duration _latency = Duration(milliseconds: 350);

  late final Map<String, Tour> _tours;

  static Tour _tour({
    required String id,
    required DateTime date,
    required String plate,
    required String status,
    required int weightKg,
    required double loadMeters,
    required List<(String, String, String)> stops,
  }) =>
      Tour(
        id: id,
        tourDate: date,
        vehicleId: 'demo-vehicle-$plate',
        vehicleLicensePlate: plate,
        driverId: 'demo-driver-1',
        driverName: 'Max Mustermann',
        status: status,
        totalWeightKg: weightKg,
        totalLoadMeters: loadMeters,
        vehiclePayloadKg: 24000,
        vehicleLoadMeters: 13.6,
        stops: <TourStop>[
          for (final (int index, (String, String, String) stop)
              in stops.indexed)
            TourStop(
              sequence: index + 1,
              transportOrderId: stop.$3,
              orderNumber: stop.$1,
              stopType: stop.$2,
            ),
        ],
      );

  @override
  Future<List<Tour>> listMyTours() async {
    await Future<void>.delayed(_latency);
    return _tours.values.toList();
  }

  @override
  Future<Tour> getTour(String id) async {
    await Future<void>.delayed(_latency);
    return _require(id);
  }

  @override
  Future<Tour> startTour(String id) async {
    await Future<void>.delayed(_latency);
    final Tour tour = _require(id);
    if (!tour.isPlanned) {
      throw ApiException(
        statusCode: 409,
        message: 'Nur geplante Touren können gestartet werden.',
      );
    }
    return _transition(tour, 'InProgress');
  }

  @override
  Future<Tour> completeTour(String id) async {
    await Future<void>.delayed(_latency);
    final Tour tour = _require(id);
    if (!tour.isInProgress) {
      throw ApiException(
        statusCode: 409,
        message: 'Nur laufende Touren können abgeschlossen werden.',
      );
    }
    return _transition(tour, 'Completed');
  }

  @override
  Future<Driver?> findMyDriver(String? externalUserId) async {
    await Future<void>.delayed(_latency);
    return Driver(
      id: 'demo-driver-1',
      firstName: 'Max',
      lastName: 'Mustermann',
      licenseClasses: const <String>['C', 'CE'],
      licenseValidUntil: DateTime(DateTime.now().year + 2, 5, 31),
      status: 'Available',
      externalUserId: 'demo-user',
    );
  }

  @override
  Future<TourRoute> getRoute(String id) async {
    await Future<void>.delayed(_latency);
    final Tour tour = _require(id);
    return switch (id) {
      'demo-tour-1' => _route(
          tour,
          stops: const <(String, String, String, double, double)>[
            ('TB-2025-0142', 'Pickup', 'Landsberg am Lech', 48.0486, 10.8715),
            ('TB-2025-0142', 'Delivery', 'München', 48.1374, 11.5755),
            ('TB-2025-0147', 'Pickup', 'München-Ost', 48.1279, 11.6049),
            ('TB-2025-0147', 'Delivery', 'Augsburg', 48.3705, 10.8978),
          ],
          waypoints: const <(double, double)>[
            (48.0486, 10.8715),
            (48.0712, 11.0034),
            (48.1031, 11.2211),
            (48.1215, 11.4216),
            (48.1374, 11.5755),
            (48.1301, 11.5920),
            (48.1279, 11.6049),
            (48.1711, 11.5120),
            (48.2258, 11.3309),
            (48.2906, 11.1203),
            (48.3705, 10.8978),
          ],
          distanceKm: 152.4,
          drivingMinutes: 158,
          violations: const <RouteViolation>[],
        ),
      'demo-tour-2' => _route(
          tour,
          stops: const <(String, String, String, double, double)>[
            ('TB-2025-0150', 'Pickup', 'Landsberg am Lech', 48.0486, 10.8715),
            ('TB-2025-0150', 'Delivery', 'Ingolstadt', 48.7651, 11.4237),
          ],
          waypoints: const <(double, double)>[
            (48.0486, 10.8715),
            (48.1689, 10.9021),
            (48.3705, 10.8978),
            (48.4521, 11.0101),
            (48.5664, 11.1839),
            (48.6911, 11.3302),
            (48.7651, 11.4237),
          ],
          distanceKm: 119.8,
          drivingMinutes: 117,
          violations: const <RouteViolation>[
            // Shows how the app renders planner warnings.
          ],
        ),
      _ => _route(
          tour,
          stops: const <(String, String, String, double, double)>[
            ('TB-2025-0128', 'Pickup', 'Landsberg am Lech', 48.0486, 10.8715),
            ('TB-2025-0131', 'Pickup', 'München', 48.1374, 11.5755),
            ('TB-2025-0128', 'Delivery', 'Rosenheim', 47.8561, 12.1289),
            ('TB-2025-0131', 'Delivery', 'Rosenheim-Süd', 47.8402, 12.1201),
          ],
          waypoints: const <(double, double)>[
            (48.0486, 10.8715),
            (48.1031, 11.2211),
            (48.1374, 11.5755),
            (48.0612, 11.7501),
            (47.9608, 11.9412),
            (47.8561, 12.1289),
            (47.8402, 12.1201),
          ],
          distanceKm: 174.6,
          drivingMinutes: 182,
          violations: const <RouteViolation>[],
        ),
    };
  }

  Tour _require(String id) {
    final Tour? tour = _tours[id];
    if (tour == null) {
      throw ApiException(statusCode: 404, message: 'Tour nicht gefunden.');
    }
    return tour;
  }

  Tour _transition(Tour tour, String status) {
    final Tour updated = Tour(
      id: tour.id,
      tourDate: tour.tourDate,
      vehicleId: tour.vehicleId,
      vehicleLicensePlate: tour.vehicleLicensePlate,
      driverId: tour.driverId,
      driverName: tour.driverName,
      status: status,
      totalWeightKg: tour.totalWeightKg,
      totalLoadMeters: tour.totalLoadMeters,
      vehiclePayloadKg: tour.vehiclePayloadKg,
      vehicleLoadMeters: tour.vehicleLoadMeters,
      stops: tour.stops,
    );
    _tours[tour.id] = updated;
    return updated;
  }

  TourRoute _route(
    Tour tour, {
    required List<(String, String, String, double, double)> stops,
    required List<(double, double)> waypoints,
    required double distanceKm,
    required int drivingMinutes,
    required List<RouteViolation> violations,
  }) {
    final DateTime departure = DateTime(
      tour.tourDate.year,
      tour.tourDate.month,
      tour.tourDate.day,
      7,
      30,
    );
    final List<ScheduleEntry> schedule = <ScheduleEntry>[];
    DateTime clock = departure;
    final int legs = stops.length - 1;
    final Duration legDriving = Duration(minutes: drivingMinutes ~/ legs);
    for (int i = 0; i < stops.length; i++) {
      final DateTime serviceEnd = clock.add(const Duration(minutes: 30));
      schedule.add(ScheduleEntry(
        kind: 'Service',
        start: clock,
        end: serviceEnd,
        countryCode: 'DE',
      ));
      clock = serviceEnd;
      if (i < legs) {
        final DateTime legEnd = clock.add(legDriving);
        schedule.add(ScheduleEntry(
          kind: 'Driving',
          start: clock,
          end: legEnd,
          countryCode: 'DE',
        ));
        clock = legEnd;
        if (i == 0 && drivingMinutes > 150) {
          final DateTime breakEnd = clock.add(const Duration(minutes: 45));
          schedule.add(ScheduleEntry(kind: 'Break', start: clock, end: breakEnd));
          clock = breakEnd;
        }
      }
    }
    return TourRoute(
      tourId: tour.id,
      departure: departure,
      end: clock,
      earliestNextStart: clock.add(const Duration(hours: 11)),
      distanceMeters: distanceKm * 1000,
      drivingSeconds: drivingMinutes * 60.0,
      stops: <RouteStop>[
        for (final (int index, (String, String, String, double, double) s)
            in stops.indexed)
          RouteStop(
            sequence: index + 1,
            orderNumber: s.$1,
            stopType: s.$2,
            countryCode: 'DE',
            place: s.$3,
            latitude: s.$4,
            longitude: s.$5,
          ),
      ],
      geometry: <GeoPoint>[
        for (final (double, double) w in waypoints)
          GeoPoint(latitude: w.$1, longitude: w.$2),
      ],
      schedule: schedule,
      violations: violations,
    );
  }
}
