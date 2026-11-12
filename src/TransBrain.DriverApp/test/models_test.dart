import 'package:flutter_test/flutter_test.dart';
import 'package:transbrain_driver/core/models/paged_result.dart';
import 'package:transbrain_driver/core/models/tour.dart';
import 'package:transbrain_driver/core/models/tour_route.dart';

void main() {
  group('Tour', () {
    test('fromJson_apiShape_mapsAllFields', () {
      final Tour tour = Tour.fromJson(<String, dynamic>{
        'id': 't-1',
        'tourDate': '2025-11-12',
        'vehicleId': 'v-1',
        'vehicleLicensePlate': 'LL-TB 101',
        'driverId': 'd-1',
        'driverName': 'Max Mustermann',
        'status': 'Planned',
        'totalWeightKg': 14800,
        'totalLoadMeters': 9.2,
        'vehiclePayloadKg': 24000,
        'vehicleLoadMeters': 13.6,
        'stops': <Map<String, dynamic>>[
          <String, dynamic>{
            'sequence': 1,
            'transportOrderId': 'o-1',
            'orderNumber': 'TB-2025-0142',
            'stopType': 'Pickup',
          },
        ],
      });

      expect(tour.id, 't-1');
      expect(tour.tourDate, DateTime(2025, 11, 12));
      expect(tour.isPlanned, isTrue);
      expect(tour.stops.single.isPickup, isTrue);
      expect(tour.totalLoadMeters, 9.2);
    });
  });

  group('TourRoute', () {
    test('fromJson_apiShape_mapsStopsGeometryScheduleViolations', () {
      final TourRoute route = TourRoute.fromJson(<String, dynamic>{
        'tourId': 't-1',
        'departure': '2025-11-12T07:30:00+01:00',
        'end': '2025-11-12T12:05:00+01:00',
        'earliestNextStart': '2025-11-12T23:05:00+01:00',
        'distanceMeters': 152400.0,
        'drivingSeconds': 9480.0,
        'stops': <Map<String, dynamic>>[
          <String, dynamic>{
            'sequence': 1,
            'orderNumber': 'TB-2025-0142',
            'stopType': 'Delivery',
            'countryCode': 'DE',
            'place': 'München',
            'latitude': 48.1374,
            'longitude': 11.5755,
          },
        ],
        'geometry': <Map<String, dynamic>>[
          <String, dynamic>{'latitude': 48.0, 'longitude': 10.8},
          <String, dynamic>{'latitude': 48.1, 'longitude': 11.5},
        ],
        'schedule': <Map<String, dynamic>>[
          <String, dynamic>{
            'kind': 'Driving',
            'start': '2025-11-12T08:00:00+01:00',
            'end': '2025-11-12T09:30:00+01:00',
            'countryCode': 'DE',
          },
        ],
        'violations': <Map<String, dynamic>>[
          <String, dynamic>{
            'severity': 'Warning',
            'code': 'DailyDrivingNearLimit',
            'message': 'Lenkzeit nahe am Tageslimit.',
            'countryCode': null,
          },
        ],
      });

      expect(route.stops.single.isPickup, isFalse);
      expect(route.geometry, hasLength(2));
      expect(route.schedule.single.duration, const Duration(minutes: 90));
      expect(route.violations.single.isViolation, isFalse);
      expect(route.distanceMeters, 152400.0);
    });
  });

  group('PagedResult', () {
    test('fromJson_moreItemsThanPage_hasMore', () {
      final PagedResult<Tour> result = PagedResult<Tour>.fromJson(
        <String, dynamic>{
          'items': <dynamic>[],
          'page': 1,
          'pageSize': 20,
          'totalCount': 45,
        },
        Tour.fromJson,
      );
      expect(result.hasMore, isTrue);
    });

    test('fromJson_lastPage_hasNoMore', () {
      final PagedResult<Tour> result = PagedResult<Tour>.fromJson(
        <String, dynamic>{
          'items': <dynamic>[],
          'page': 3,
          'pageSize': 20,
          'totalCount': 45,
        },
        Tour.fromJson,
      );
      expect(result.hasMore, isFalse);
    });
  });
}
