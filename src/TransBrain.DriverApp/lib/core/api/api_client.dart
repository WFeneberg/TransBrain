import 'dart:convert';

import 'package:http/http.dart' as http;

import '../auth/auth_service.dart';
import 'api_exception.dart';

/// Thin JSON client for the TransBrain API: attaches the bearer token, decodes
/// responses and turns problem+json errors into [ApiException]s with readable
/// German messages.
class ApiClient {
  ApiClient({
    required this.baseUrl,
    required this._auth,
    http.Client? httpClient,
  }) : _http = httpClient ?? http.Client();

  final String baseUrl;
  final AuthService _auth;
  final http.Client _http;

  Future<dynamic> getJson(String path, [Map<String, String>? query]) async {
    final http.Response response =
        await _http.get(_uri(path, query), headers: await _headers());
    return _decode(response);
  }

  Future<dynamic> postJson(String path, {Object? body}) async {
    final http.Response response = await _http.post(
      _uri(path, null),
      headers: await _headers(json: body != null),
      body: body == null ? null : jsonEncode(body),
    );
    return _decode(response);
  }

  Uri _uri(String path, Map<String, String>? query) {
    final Uri base = Uri.parse(baseUrl);
    return base.replace(
      path: '${base.path.replaceAll(RegExp(r'/$'), '')}$path',
      queryParameters: query == null || query.isEmpty ? null : query,
    );
  }

  Future<Map<String, String>> _headers({bool json = false}) async {
    final String? token = await _auth.accessToken();
    return <String, String>{
      'Accept': 'application/json',
      if (json) 'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };
  }

  dynamic _decode(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.body.isEmpty) {
        return null;
      }
      return json.decode(utf8.decode(response.bodyBytes));
    }
    throw ApiException(
      statusCode: response.statusCode,
      message: _errorMessage(response),
    );
  }

  String _errorMessage(http.Response response) {
    String fallback() => switch (response.statusCode) {
          401 => 'Nicht angemeldet oder Sitzung abgelaufen.',
          403 => 'Keine Berechtigung für diese Aktion.',
          404 => 'Nicht gefunden.',
          409 => 'Die Aktion ist im aktuellen Zustand nicht möglich.',
          503 => 'Ein benötigter Dienst ist gerade nicht erreichbar.',
          _ => 'Unerwarteter Fehler (HTTP ${response.statusCode}).',
        };
    try {
      final Map<String, dynamic> problem =
          json.decode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>;
      final String? detail = problem['detail'] as String?;
      final String? title = problem['title'] as String?;
      final Map<String, dynamic>? errors =
          problem['errors'] as Map<String, dynamic>?;
      if (errors != null && errors.isNotEmpty) {
        final List<String> messages = errors.values
            .expand((dynamic v) => (v as List<dynamic>).cast<String>())
            .toList();
        if (messages.isNotEmpty) {
          return messages.join('\n');
        }
      }
      if (detail != null && detail.isNotEmpty) {
        return detail;
      }
      if (title != null && title.isNotEmpty) {
        return title;
      }
      return fallback();
    } on Object {
      return fallback();
    }
  }
}
