/// One stop of a tour as the planning sees it: which order, pickup or
/// delivery, in which position.
class TourStop {
  TourStop({
    required this.sequence,
    required this.transportOrderId,
    required this.orderNumber,
    required this.stopType,
  });

  final int sequence;
  final String transportOrderId;
  final String orderNumber;

  /// `Pickup` or `Delivery` (API enum names).
  final String stopType;

  bool get isPickup => stopType == 'Pickup';

  factory TourStop.fromJson(Map<String, dynamic> json) => TourStop(
        sequence: (json['sequence'] as num).toInt(),
        transportOrderId: json['transportOrderId'] as String,
        orderNumber: json['orderNumber'] as String,
        stopType: json['stopType'] as String,
      );
}

/// A tour as returned by `/api/tours`: one vehicle, one driver, one day,
/// ordered stops and the load against the vehicle's rating.
class Tour {
  Tour({
    required this.id,
    required this.tourDate,
    required this.vehicleId,
    required this.vehicleLicensePlate,
    required this.driverId,
    required this.driverName,
    required this.status,
    required this.totalWeightKg,
    required this.totalLoadMeters,
    required this.vehiclePayloadKg,
    required this.vehicleLoadMeters,
    required this.stops,
  });

  final String id;
  final DateTime tourDate;
  final String vehicleId;
  final String vehicleLicensePlate;
  final String driverId;
  final String driverName;

  /// `Planned`, `InProgress` or `Completed` (API enum names).
  final String status;
  final int totalWeightKg;
  final double totalLoadMeters;
  final int vehiclePayloadKg;
  final double vehicleLoadMeters;
  final List<TourStop> stops;

  bool get isPlanned => status == 'Planned';
  bool get isInProgress => status == 'InProgress';
  bool get isCompleted => status == 'Completed';

  factory Tour.fromJson(Map<String, dynamic> json) => Tour(
        id: json['id'] as String,
        tourDate: DateTime.parse(json['tourDate'] as String),
        vehicleId: json['vehicleId'] as String,
        vehicleLicensePlate: json['vehicleLicensePlate'] as String,
        driverId: json['driverId'] as String,
        driverName: json['driverName'] as String,
        status: json['status'] as String,
        totalWeightKg: (json['totalWeightKg'] as num).toInt(),
        totalLoadMeters: (json['totalLoadMeters'] as num).toDouble(),
        vehiclePayloadKg: (json['vehiclePayloadKg'] as num).toInt(),
        vehicleLoadMeters: (json['vehicleLoadMeters'] as num).toDouble(),
        stops: (json['stops'] as List<dynamic>? ?? const <dynamic>[])
            .map((dynamic s) => TourStop.fromJson(s as Map<String, dynamic>))
            .toList(),
      );
}
