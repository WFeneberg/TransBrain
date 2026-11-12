import 'dart:async';

/// The platform-specific half of the login: getting an authorization code into
/// the app. Web leaves the page and returns via redirect; iOS/Android open the
/// system browser and return via the app's custom URL scheme.
abstract class AuthFlowDelegate {
  /// Where Keycloak sends the browser after login.
  String get redirectUri;

  /// Sends the user to the hosted login page. On web this navigates away from
  /// the running app, so [state] and [codeVerifier] must survive the reload.
  Future<void> launchAuthorization(
    Uri authorizationUrl,
    String state,
    String codeVerifier,
  );

  /// A callback that arrived before the app started — the web page reloading
  /// on the redirect URI. Null when the app started normally.
  Future<AuthCallback?> consumePendingCallback();

  /// Callbacks arriving while the app is running — mobile deep links.
  Stream<AuthCallback> get callbacks;

  void dispose();
}

/// An authorization code paired with the PKCE verifier of the request that
/// produced it, ready for the token exchange.
class AuthCallback {
  AuthCallback({required this.code, required this.codeVerifier});

  final String code;
  final String codeVerifier;
}
