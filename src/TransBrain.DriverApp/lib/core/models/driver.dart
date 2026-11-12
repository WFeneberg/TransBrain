/// A driver master-data record; the app shows the one whose `externalUserId`
/// matches the signed-in user.
class Driver {
  Driver({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.licenseClasses,
    required this.licenseValidUntil,
    required this.status,
    this.externalUserId,
  });

  final String id;
  final String firstName;
  final String lastName;
  final List<String> licenseClasses;
  final DateTime licenseValidUntil;
  final String status;
  final String? externalUserId;

  String get fullName => '$firstName $lastName';

  factory Driver.fromJson(Map<String, dynamic> json) => Driver(
        id: json['id'] as String,
        firstName: json['firstName'] as String,
        lastName: json['lastName'] as String,
        licenseClasses:
            (json['licenseClasses'] as List<dynamic>? ?? const <dynamic>[])
                .cast<String>(),
        licenseValidUntil: DateTime.parse(json['licenseValidUntil'] as String),
        status: json['status'] as String,
        externalUserId: json['externalUserId'] as String?,
      );
}
