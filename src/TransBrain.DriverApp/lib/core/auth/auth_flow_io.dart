import 'dart:async';

import 'package:app_links/app_links.dart';
import 'package:url_launcher/url_launcher.dart';

import '../config/app_config.dart';
import 'auth_flow_types.dart';

AuthFlowDelegate createAuthFlowDelegate() => IoAuthFlowDelegate();

/// Mobile flow: the system browser (custom tab / ASWebAuthenticationSession
/// sibling) shows the login page; Keycloak redirects to the app's custom URL
/// scheme, which arrives here as a deep link.
class IoAuthFlowDelegate implements AuthFlowDelegate {
  IoAuthFlowDelegate() {
    _subscription = _appLinks.uriLinkStream.listen(_onUri);
  }

  final AppLinks _appLinks = AppLinks();
  final StreamController<AuthCallback> _controller =
      StreamController<AuthCallback>.broadcast();
  StreamSubscription<Uri>? _subscription;
  String? _pendingState;
  String? _pendingVerifier;

  @override
  String get redirectUri => AppConfig.mobileRedirectUri;

  @override
  Future<void> launchAuthorization(
    Uri authorizationUrl,
    String state,
    String codeVerifier,
  ) async {
    _pendingState = state;
    _pendingVerifier = codeVerifier;
    final bool launched = await launchUrl(
      authorizationUrl,
      mode: LaunchMode.externalApplication,
    );
    if (!launched) {
      throw StateError('Browser für die Anmeldung konnte nicht geöffnet werden.');
    }
  }

  @override
  Future<AuthCallback?> consumePendingCallback() async => null;

  @override
  Stream<AuthCallback> get callbacks => _controller.stream;

  void _onUri(Uri uri) {
    final Uri expected = Uri.parse(redirectUri);
    if (uri.scheme != expected.scheme) {
      return;
    }
    final String? code = uri.queryParameters['code'];
    final String? state = uri.queryParameters['state'];
    final String? verifier = _pendingVerifier;
    if (code == null || verifier == null || state != _pendingState) {
      return;
    }
    _pendingState = null;
    _pendingVerifier = null;
    _controller.add(AuthCallback(code: code, codeVerifier: verifier));
  }

  @override
  void dispose() {
    _subscription?.cancel();
    _controller.close();
  }
}
