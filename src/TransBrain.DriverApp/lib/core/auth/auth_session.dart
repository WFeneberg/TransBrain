import 'dart:convert';

/// The signed-in user as the app needs them: tokens for the API, identity
/// claims for display and the demo flag for offline operation.
class AuthSession {
  AuthSession({
    required this.accessToken,
    required this.expiresAt,
    required this.userName,
    this.refreshToken,
    this.idToken,
    this.userId,
    this.roles = const <String>[],
    this.isDemo = false,
  });

  final String accessToken;
  final String? refreshToken;
  final String? idToken;
  final DateTime expiresAt;

  /// Keycloak `sub` claim — what a driver's `ExternalUserId` stores.
  final String? userId;
  final String userName;
  final List<String> roles;
  final bool isDemo;

  /// Expired (with a safety margin) and in need of a refresh before API calls.
  bool get needsRefresh =>
      !isDemo &&
      DateTime.now().isAfter(expiresAt.subtract(const Duration(seconds: 30)));

  bool hasRole(String role) => roles.contains(role);

  factory AuthSession.demo() => AuthSession(
        accessToken: 'demo',
        expiresAt: DateTime.now().add(const Duration(days: 365)),
        userId: 'demo-user',
        userName: 'max.mustermann',
        roles: const <String>['fahrer'],
        isDemo: true,
      );

  /// Builds a session from a Keycloak token-endpoint response.
  factory AuthSession.fromTokenResponse(Map<String, dynamic> json) {
    final String accessToken = json['access_token'] as String;
    final Map<String, dynamic> claims = decodeJwtPayload(accessToken);
    final int expiresIn = (json['expires_in'] as num?)?.toInt() ?? 300;
    final Map<String, dynamic>? realmAccess =
        claims['realm_access'] as Map<String, dynamic>?;
    final List<String> roles = (realmAccess?['roles'] as List<dynamic>? ??
            const <dynamic>[])
        .cast<String>();
    return AuthSession(
      accessToken: accessToken,
      refreshToken: json['refresh_token'] as String?,
      idToken: json['id_token'] as String?,
      expiresAt: DateTime.now().add(Duration(seconds: expiresIn)),
      userId: claims['sub'] as String?,
      userName: (claims['preferred_username'] as String?) ??
          (claims['sub'] as String?) ??
          'unbekannt',
      roles: roles,
    );
  }
}

/// Decodes the payload segment of a JWT without verifying the signature —
/// verification is the API's job; the app only reads display claims.
Map<String, dynamic> decodeJwtPayload(String jwt) {
  final List<String> parts = jwt.split('.');
  if (parts.length != 3) {
    throw const FormatException('Not a JWT');
  }
  final String normalized = base64Url.normalize(parts[1]);
  return json.decode(utf8.decode(base64Url.decode(normalized)))
      as Map<String, dynamic>;
}
