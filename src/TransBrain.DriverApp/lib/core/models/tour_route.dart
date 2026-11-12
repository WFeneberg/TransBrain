/// A geocoded position on the planned route.
class GeoPoint {
  const GeoPoint({required this.latitude, required this.longitude});

  final double latitude;
  final double longitude;

  factory GeoPoint.fromJson(Map<String, dynamic> json) => GeoPoint(
        latitude: (json['latitude'] as num).toDouble(),
        longitude: (json['longitude'] as num).toDouble(),
      );
}

/// A tour stop with its place and position, as the routing sees it.
class RouteStop {
  RouteStop({
    required this.sequence,
    required this.orderNumber,
    required this.stopType,
    required this.countryCode,
    required this.place,
    required this.latitude,
    required this.longitude,
  });

  final int sequence;
  final String orderNumber;
  final String stopType;
  final String countryCode;
  final String place;
  final double latitude;
  final double longitude;

  bool get isPickup => stopType == 'Pickup';

  factory RouteStop.fromJson(Map<String, dynamic> json) => RouteStop(
        sequence: (json['sequence'] as num).toInt(),
        orderNumber: json['orderNumber'] as String,
        stopType: json['stopType'] as String,
        countryCode: json['countryCode'] as String,
        place: json['place'] as String,
        latitude: (json['latitude'] as num).toDouble(),
        longitude: (json['longitude'] as num).toDouble(),
      );
}

/// One activity on the driver's planned day: `Driving`, `Break` or `Service`
/// (loading/unloading).
class ScheduleEntry {
  ScheduleEntry({
    required this.kind,
    required this.start,
    required this.end,
    this.countryCode,
  });

  final String kind;
  final DateTime start;
  final DateTime end;
  final String? countryCode;

  Duration get duration => end.difference(start);

  factory ScheduleEntry.fromJson(Map<String, dynamic> json) => ScheduleEntry(
        kind: json['kind'] as String,
        start: DateTime.parse(json['start'] as String),
        end: DateTime.parse(json['end'] as String),
        countryCode: json['countryCode'] as String?,
      );
}

/// A driving-time rule the planned day breaks (`Violation`) or grazes
/// (`Warning`).
class RouteViolation {
  RouteViolation({
    required this.severity,
    required this.code,
    required this.message,
    this.countryCode,
  });

  final String severity;
  final String code;
  final String message;
  final String? countryCode;

  bool get isViolation => severity == 'Violation';

  factory RouteViolation.fromJson(Map<String, dynamic> json) => RouteViolation(
        severity: json['severity'] as String,
        code: json['code'] as String,
        message: json['message'] as String,
        countryCode: json['countryCode'] as String?,
      );
}

/// The planned road route of a tour plus the driver's timeline over it, from
/// `/api/tours/{id}/route`.
class TourRoute {
  TourRoute({
    required this.tourId,
    required this.departure,
    required this.end,
    required this.earliestNextStart,
    required this.distanceMeters,
    required this.drivingSeconds,
    required this.stops,
    required this.geometry,
    required this.schedule,
    required this.violations,
  });

  final String tourId;
  final DateTime departure;
  final DateTime end;
  final DateTime earliestNextStart;
  final double distanceMeters;
  final double drivingSeconds;
  final List<RouteStop> stops;
  final List<GeoPoint> geometry;
  final List<ScheduleEntry> schedule;
  final List<RouteViolation> violations;

  factory TourRoute.fromJson(Map<String, dynamic> json) => TourRoute(
        tourId: json['tourId'] as String,
        departure: DateTime.parse(json['departure'] as String),
        end: DateTime.parse(json['end'] as String),
        earliestNextStart: DateTime.parse(json['earliestNextStart'] as String),
        distanceMeters: (json['distanceMeters'] as num).toDouble(),
        drivingSeconds: (json['drivingSeconds'] as num).toDouble(),
        stops: (json['stops'] as List<dynamic>? ?? const <dynamic>[])
            .map((dynamic s) => RouteStop.fromJson(s as Map<String, dynamic>))
            .toList(),
        geometry: (json['geometry'] as List<dynamic>? ?? const <dynamic>[])
            .map((dynamic p) => GeoPoint.fromJson(p as Map<String, dynamic>))
            .toList(),
        schedule: (json['schedule'] as List<dynamic>? ?? const <dynamic>[])
            .map((dynamic e) =>
                ScheduleEntry.fromJson(e as Map<String, dynamic>))
            .toList(),
        violations: (json['violations'] as List<dynamic>? ?? const <dynamic>[])
            .map((dynamic v) =>
                RouteViolation.fromJson(v as Map<String, dynamic>))
            .toList(),
      );
}
