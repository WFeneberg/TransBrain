import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:transbrain_driver/core/auth/auth_session.dart';
import 'package:transbrain_driver/core/auth/oidc_client.dart';

import 'helpers.dart';

void main() {
  group('OidcClient', () {
    test('authorizationUrl_always_containsPkceAndRedirect', () {
      final OidcClient client = OidcClient(
        authority: 'https://localhost:8080/realms/transbrain',
        clientId: 'transbrain-mobile',
      );
      final Uri url = client.authorizationUrl(
        redirectUri: 'http://localhost:4400/',
        state: 'state-1',
        codeChallenge: 'challenge-1',
      );

      expect(url.path, '/realms/transbrain/protocol/openid-connect/auth');
      expect(url.queryParameters['client_id'], 'transbrain-mobile');
      expect(url.queryParameters['response_type'], 'code');
      expect(url.queryParameters['code_challenge'], 'challenge-1');
      expect(url.queryParameters['code_challenge_method'], 'S256');
      expect(url.queryParameters['redirect_uri'], 'http://localhost:4400/');
      expect(url.queryParameters['state'], 'state-1');
    });

    test('exchangeCode_success_sendsGrantAndParsesSession', () async {
      late Map<String, String> sentBody;
      final OidcClient client = OidcClient(
        authority: 'https://kc/realms/transbrain',
        clientId: 'transbrain-mobile',
        httpClient: MockClient((http.Request request) async {
          expect(
            request.url.path,
            '/realms/transbrain/protocol/openid-connect/token',
          );
          sentBody = Uri.splitQueryString(request.body);
          return http.Response(
            json.encode(<String, dynamic>{
              'access_token': makeJwt(<String, dynamic>{
                'sub': 'user-1',
                'preferred_username': 'fahrer.user',
              }),
              'refresh_token': 'refresh-1',
              'expires_in': 300,
            }),
            200,
            headers: <String, String>{
              'content-type': 'application/json',
            },
          );
        }),
      );

      final AuthSession session = await client.exchangeCode(
        code: 'code-1',
        redirectUri: 'http://localhost:4400/',
        codeVerifier: 'verifier-1',
      );

      expect(sentBody['grant_type'], 'authorization_code');
      expect(sentBody['code'], 'code-1');
      expect(sentBody['code_verifier'], 'verifier-1');
      expect(session.userName, 'fahrer.user');
      expect(session.refreshToken, 'refresh-1');
    });

    test('refresh_serverRejects_throwsAuthException', () async {
      final OidcClient client = OidcClient(
        authority: 'https://kc/realms/transbrain',
        clientId: 'transbrain-mobile',
        httpClient: MockClient(
          (http.Request request) async => http.Response('{}', 400),
        ),
      );

      await expectLater(
        client.refresh('expired-token'),
        throwsA(isA<AuthException>()),
      );
    });
  });
}
