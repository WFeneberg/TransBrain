import 'dart:convert';

import 'package:http/http.dart' as http;

import 'auth_session.dart';

/// Talks OIDC (authorization code + PKCE) to Keycloak. The authorize step runs
/// in a browser — built here, launched by the platform's [AuthFlowDelegate];
/// the token exchange and refresh are plain HTTP and shared by all platforms.
class OidcClient {
  OidcClient({
    required this.authority,
    required this.clientId,
    http.Client? httpClient,
  }) : _http = httpClient ?? http.Client();

  final String authority;
  final String clientId;
  final http.Client _http;

  Uri get _authorizeEndpoint =>
      Uri.parse('$authority/protocol/openid-connect/auth');
  Uri get _tokenEndpoint =>
      Uri.parse('$authority/protocol/openid-connect/token');
  Uri get _logoutEndpoint =>
      Uri.parse('$authority/protocol/openid-connect/logout');

  Uri authorizationUrl({
    required String redirectUri,
    required String state,
    required String codeChallenge,
  }) =>
      _authorizeEndpoint.replace(queryParameters: <String, String>{
        'client_id': clientId,
        'response_type': 'code',
        'scope': 'openid profile',
        'redirect_uri': redirectUri,
        'state': state,
        'code_challenge': codeChallenge,
        'code_challenge_method': 'S256',
      });

  Future<AuthSession> exchangeCode({
    required String code,
    required String redirectUri,
    required String codeVerifier,
  }) =>
      _requestToken(<String, String>{
        'grant_type': 'authorization_code',
        'client_id': clientId,
        'code': code,
        'redirect_uri': redirectUri,
        'code_verifier': codeVerifier,
      });

  Future<AuthSession> refresh(String refreshToken) =>
      _requestToken(<String, String>{
        'grant_type': 'refresh_token',
        'client_id': clientId,
        'refresh_token': refreshToken,
      });

  /// Ends the Keycloak session server-side. Best effort: local sign-out must
  /// succeed even when Keycloak is unreachable.
  Future<void> revoke(String refreshToken) async {
    try {
      await _http.post(
        _logoutEndpoint,
        headers: const <String, String>{
          'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: <String, String>{
          'client_id': clientId,
          'refresh_token': refreshToken,
        },
      );
    } on Exception {
      // Ignored: the local session is dropped regardless.
    }
  }

  Future<AuthSession> _requestToken(Map<String, String> body) async {
    final http.Response response = await _http.post(
      _tokenEndpoint,
      headers: const <String, String>{
        'Content-Type': 'application/x-www-form-urlencoded',
      },
      body: body,
    );
    if (response.statusCode != 200) {
      throw AuthException(
        'Token-Anfrage fehlgeschlagen (HTTP ${response.statusCode}).',
      );
    }
    return AuthSession.fromTokenResponse(
      json.decode(response.body) as Map<String, dynamic>,
    );
  }
}

class AuthException implements Exception {
  AuthException(this.message);

  final String message;

  @override
  String toString() => message;
}
