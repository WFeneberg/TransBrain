import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'auth_flow_stub.dart'
    if (dart.library.js_interop) 'auth_flow_web.dart'
    if (dart.library.io) 'auth_flow_io.dart' as platform;
import 'auth_flow_types.dart';
import 'auth_session.dart';
import 'oidc_client.dart';
import 'pkce.dart';

/// Owns the session: sign-in (Keycloak or demo), restore on start, token
/// refresh and sign-out. Screens listen to it to switch between login and app.
class AuthService extends ChangeNotifier {
  AuthService({
    required this._oidc,
    required SharedPreferences preferences,
    AuthFlowDelegate? flowDelegate,
  })  : _prefs = preferences,
        _delegate = flowDelegate ?? platform.createAuthFlowDelegate();

  static const String _refreshTokenKey = 'tb.auth.refreshToken';
  static const String _demoKey = 'tb.auth.demo';

  final OidcClient _oidc;
  final SharedPreferences _prefs;
  final AuthFlowDelegate _delegate;
  StreamSubscription<AuthCallback>? _callbackSubscription;

  AuthSession? _session;
  bool _initialized = false;
  String? _lastError;

  AuthSession? get session => _session;
  bool get isAuthenticated => _session != null;
  bool get isInitialized => _initialized;
  bool get isDemo => _session?.isDemo ?? false;

  /// The failure of the most recent sign-in or restore attempt, for the login
  /// screen to show. Cleared by the next attempt.
  String? get lastError => _lastError;

  /// Restores a session on app start: a web redirect callback, a persisted
  /// demo session, or a persisted refresh token — in that order.
  Future<void> initialize() async {
    _callbackSubscription = _delegate.callbacks.listen(_completeLogin);
    try {
      final AuthCallback? callback = await _delegate.consumePendingCallback();
      if (callback != null) {
        await _completeLogin(callback);
        return;
      }
      if (_prefs.getBool(_demoKey) ?? false) {
        _session = AuthSession.demo();
        return;
      }
      final String? refreshToken = _prefs.getString(_refreshTokenKey);
      if (refreshToken != null) {
        try {
          _session = await _oidc.refresh(refreshToken);
          await _persistRefreshToken();
        } on Exception {
          await _prefs.remove(_refreshTokenKey);
        }
      }
    } finally {
      _initialized = true;
      notifyListeners();
    }
  }

  /// Starts the Keycloak login. On web the page navigates away; on mobile the
  /// result arrives later through the deep-link stream.
  Future<void> signIn() async {
    _lastError = null;
    notifyListeners();
    final String state = Pkce.randomState();
    final String verifier = Pkce.randomVerifier();
    final Uri url = _oidc.authorizationUrl(
      redirectUri: _delegate.redirectUri,
      state: state,
      codeChallenge: Pkce.challengeS256(verifier),
    );
    try {
      await _delegate.launchAuthorization(url, state, verifier);
    } on Exception catch (error) {
      _lastError = error.toString();
      notifyListeners();
    }
  }

  /// Signs in against bundled demo data — no backend, no Keycloak.
  Future<void> signInDemo() async {
    _lastError = null;
    await _prefs.setBool(_demoKey, true);
    _session = AuthSession.demo();
    notifyListeners();
  }

  Future<void> signOut() async {
    final String? refreshToken = _session?.refreshToken;
    if (refreshToken != null) {
      await _oidc.revoke(refreshToken);
    }
    await _prefs.remove(_refreshTokenKey);
    await _prefs.remove(_demoKey);
    _session = null;
    notifyListeners();
  }

  /// A currently valid access token, refreshing if necessary. Null in demo
  /// mode (no API calls) and when signed out.
  Future<String?> accessToken() async {
    final AuthSession? current = _session;
    if (current == null || current.isDemo) {
      return null;
    }
    if (!current.needsRefresh) {
      return current.accessToken;
    }
    final String? refreshToken = current.refreshToken;
    if (refreshToken == null) {
      return current.accessToken;
    }
    try {
      _session = await _oidc.refresh(refreshToken);
      await _persistRefreshToken();
      notifyListeners();
      return _session!.accessToken;
    } on Exception {
      await signOut();
      return null;
    }
  }

  Future<void> _completeLogin(AuthCallback callback) async {
    try {
      _session = await _oidc.exchangeCode(
        code: callback.code,
        redirectUri: _delegate.redirectUri,
        codeVerifier: callback.codeVerifier,
      );
      await _prefs.remove(_demoKey);
      await _persistRefreshToken();
      _lastError = null;
    } on Exception catch (error) {
      _lastError = error.toString();
    }
    notifyListeners();
  }

  Future<void> _persistRefreshToken() async {
    final String? refreshToken = _session?.refreshToken;
    if (refreshToken == null) {
      await _prefs.remove(_refreshTokenKey);
    } else {
      await _prefs.setString(_refreshTokenKey, refreshToken);
    }
  }

  @override
  void dispose() {
    _callbackSubscription?.cancel();
    _delegate.dispose();
    super.dispose();
  }
}
