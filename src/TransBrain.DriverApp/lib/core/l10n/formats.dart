import 'package:intl/intl.dart';

/// German display formatting, in one place so every screen agrees.
abstract final class Formats {
  static final DateFormat weekdayDate = DateFormat('EEEE, dd.MM.yyyy', 'de_DE');
  static final DateFormat shortDate = DateFormat('dd.MM.yyyy', 'de_DE');
  static final DateFormat time = DateFormat('HH:mm', 'de_DE');
  static final NumberFormat _number = NumberFormat.decimalPattern('de_DE');
  static final NumberFormat _oneDecimal = NumberFormat('#,##0.0', 'de_DE');

  static String weightKg(num kg) => '${_number.format(kg)} kg';

  static String loadMeters(num meters) => '${_oneDecimal.format(meters)} m';

  static String km(double meters) => '${_oneDecimal.format(meters / 1000)} km';

  static String duration(Duration duration) {
    final int hours = duration.inHours;
    final int minutes = duration.inMinutes.remainder(60);
    if (hours == 0) {
      return '$minutes min';
    }
    return '$hours h $minutes min';
  }

  static String timeRange(DateTime start, DateTime end) =>
      '${time.format(start.toLocal())}–${time.format(end.toLocal())} Uhr';
}

/// German labels for the API's enum names.
abstract final class Labels {
  static String tourStatus(String status) => switch (status) {
        'Planned' => 'Geplant',
        'InProgress' => 'Unterwegs',
        'Completed' => 'Abgeschlossen',
        _ => status,
      };

  static String stopType(String stopType) => switch (stopType) {
        'Pickup' => 'Beladen',
        'Delivery' => 'Entladen',
        _ => stopType,
      };

  static String driverStatus(String status) => switch (status) {
        'Available' => 'Verfügbar',
        'Absent' => 'Abwesend',
        'Inactive' => 'Inaktiv',
        _ => status,
      };

  static String scheduleKind(String kind) => switch (kind) {
        'Driving' => 'Fahrt',
        'Break' => 'Pause',
        'Service' => 'Be-/Entladung',
        _ => kind,
      };

  static String violationSeverity(String severity) => switch (severity) {
        'Warning' => 'Hinweis',
        'Violation' => 'Verstoß',
        _ => severity,
      };
}
