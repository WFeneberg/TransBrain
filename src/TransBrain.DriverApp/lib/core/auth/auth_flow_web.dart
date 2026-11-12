import 'dart:async';

import 'package:web/web.dart' as web;

import 'auth_flow_types.dart';

AuthFlowDelegate createAuthFlowDelegate() => WebAuthFlowDelegate();

/// Web flow: a full-page redirect to Keycloak and back. The app restarts on
/// return, so state and verifier are parked in sessionStorage across the hop.
class WebAuthFlowDelegate implements AuthFlowDelegate {
  static const String _stateKey = 'tb.login.state';
  static const String _verifierKey = 'tb.login.verifier';

  @override
  String get redirectUri => '${Uri.base.origin}/';

  @override
  Future<void> launchAuthorization(
    Uri authorizationUrl,
    String state,
    String codeVerifier,
  ) async {
    web.window.sessionStorage.setItem(_stateKey, state);
    web.window.sessionStorage.setItem(_verifierKey, codeVerifier);
    web.window.location.assign(authorizationUrl.toString());
  }

  @override
  Future<AuthCallback?> consumePendingCallback() async {
    final Uri uri = Uri.base;
    final String? code = uri.queryParameters['code'];
    final String? state = uri.queryParameters['state'];
    if (code == null || state == null) {
      return null;
    }

    final String? expectedState = web.window.sessionStorage.getItem(_stateKey);
    final String? verifier = web.window.sessionStorage.getItem(_verifierKey);
    web.window.sessionStorage.removeItem(_stateKey);
    web.window.sessionStorage.removeItem(_verifierKey);

    // Strip code and state from the address bar so a reload does not retry
    // the one-time code exchange.
    web.window.history.replaceState(null, '', '${uri.origin}${uri.path}');

    if (verifier == null || state != expectedState) {
      return null;
    }
    return AuthCallback(code: code, codeVerifier: verifier);
  }

  @override
  Stream<AuthCallback> get callbacks => const Stream<AuthCallback>.empty();

  @override
  void dispose() {}
}
