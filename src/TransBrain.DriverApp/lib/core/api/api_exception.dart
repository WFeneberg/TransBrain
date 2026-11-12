/// An API call that came back as an error, carrying the RFC 7807 problem
/// details the TransBrain API responds with.
class ApiException implements Exception {
  ApiException({required this.statusCode, required this.message});

  final int statusCode;
  final String message;

  bool get isForbidden => statusCode == 403;
  bool get isNotFound => statusCode == 404;
  bool get isConflict => statusCode == 409;

  @override
  String toString() => message;
}
