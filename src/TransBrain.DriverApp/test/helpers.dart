import 'dart:async';
import 'dart:convert';

import 'package:transbrain_driver/core/auth/auth_flow_types.dart';

/// Test stand-in for the platform login flow: no browser, no deep links.
class FakeAuthFlowDelegate implements AuthFlowDelegate {
  final StreamController<AuthCallback> controller =
      StreamController<AuthCallback>.broadcast();
  AuthCallback? pendingCallback;
  Uri? launchedUrl;

  @override
  String get redirectUri => 'http://localhost:4400/';

  @override
  Future<void> launchAuthorization(
    Uri authorizationUrl,
    String state,
    String codeVerifier,
  ) async {
    launchedUrl = authorizationUrl;
  }

  @override
  Future<AuthCallback?> consumePendingCallback() async => pendingCallback;

  @override
  Stream<AuthCallback> get callbacks => controller.stream;

  @override
  void dispose() {
    controller.close();
  }
}

/// An unsigned JWT with the given payload — enough for claim parsing, which
/// never verifies signatures client-side.
String makeJwt(Map<String, dynamic> payload) {
  String encode(Map<String, dynamic> part) =>
      base64UrlEncode(utf8.encode(json.encode(part))).replaceAll('=', '');
  return '${encode(<String, dynamic>{'alg': 'none'})}.${encode(payload)}.sig';
}
