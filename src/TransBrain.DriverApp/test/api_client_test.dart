import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:transbrain_driver/core/api/api_client.dart';
import 'package:transbrain_driver/core/api/api_exception.dart';
import 'package:transbrain_driver/core/auth/auth_service.dart';
import 'package:transbrain_driver/core/auth/oidc_client.dart';

import 'helpers.dart';

Future<AuthService> demoAuth() async {
  SharedPreferences.setMockInitialValues(<String, Object>{});
  final SharedPreferences prefs = await SharedPreferences.getInstance();
  final AuthService auth = AuthService(
    oidc: OidcClient(authority: 'https://kc/realms/x', clientId: 'c'),
    preferences: prefs,
    flowDelegate: FakeAuthFlowDelegate(),
  );
  await auth.initialize();
  await auth.signInDemo();
  return auth;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('ApiClient', () {
    test('getJson_success_decodesUtf8Json', () async {
      final ApiClient client = ApiClient(
        baseUrl: 'https://api.example',
        auth: await demoAuth(),
        httpClient: MockClient((http.Request request) async {
          expect(request.url.toString(),
              'https://api.example/api/tours?page=1&pageSize=50');
          return http.Response.bytes(
            utf8.encode('{"city":"München"}'),
            200,
            headers: <String, String>{'content-type': 'application/json'},
          );
        }),
      );

      final dynamic result = await client.getJson(
        '/api/tours',
        <String, String>{'page': '1', 'pageSize': '50'},
      );
      expect((result as Map<String, dynamic>)['city'], 'München');
    });

    test('getJson_problemJsonWithDetail_throwsApiExceptionWithDetail',
        () async {
      final ApiClient client = ApiClient(
        baseUrl: 'https://api.example',
        auth: await demoAuth(),
        httpClient: MockClient(
          (http.Request request) async => http.Response.bytes(
            utf8.encode(json.encode(<String, dynamic>{
              'title': 'Conflict',
              'status': 409,
              'detail': 'Nur geplante Touren können gestartet werden.',
            })),
            409,
            headers: <String, String>{
              'content-type': 'application/problem+json',
            },
          ),
        ),
      );

      await expectLater(
        client.getJson('/api/tours/x'),
        throwsA(isA<ApiException>()
            .having((ApiException e) => e.statusCode, 'statusCode', 409)
            .having((ApiException e) => e.message, 'message',
                'Nur geplante Touren können gestartet werden.')),
      );
    });

    test('getJson_validationErrors_joinsAllMessages', () async {
      final ApiClient client = ApiClient(
        baseUrl: 'https://api.example',
        auth: await demoAuth(),
        httpClient: MockClient(
          (http.Request request) async => http.Response.bytes(
            utf8.encode(json.encode(<String, dynamic>{
              'title': 'Validation failed',
              'status': 400,
              'errors': <String, dynamic>{
                'Page': <String>['Seite muss größer 0 sein.'],
                'PageSize': <String>['Seitengröße ist ungültig.'],
              },
            })),
            400,
          ),
        ),
      );

      await expectLater(
        client.getJson('/api/tours'),
        throwsA(isA<ApiException>().having(
          (ApiException e) => e.message,
          'message',
          'Seite muss größer 0 sein.\nSeitengröße ist ungültig.',
        )),
      );
    });

    test('getJson_unknownServerError_usesGermanFallback', () async {
      final ApiClient client = ApiClient(
        baseUrl: 'https://api.example',
        auth: await demoAuth(),
        httpClient: MockClient(
          (http.Request request) async => http.Response('boom', 500),
        ),
      );

      await expectLater(
        client.getJson('/api/tours'),
        throwsA(isA<ApiException>().having(
          (ApiException e) => e.message,
          'message',
          'Unerwarteter Fehler (HTTP 500).',
        )),
      );
    });
  });
}
