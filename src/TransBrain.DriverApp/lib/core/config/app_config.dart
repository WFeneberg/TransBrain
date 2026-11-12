/// Compile-time configuration, overridable per build with `--dart-define`.
///
/// Defaults match the local Aspire stack: the API's `https` launch profile and
/// the Keycloak realm imported by the AppHost. On an Android emulator the host
/// machine is reachable as `10.0.2.2`, so pass e.g.
/// `--dart-define=TB_API_BASE_URL=https://10.0.2.2:7049`.
abstract final class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'TB_API_BASE_URL',
    defaultValue: 'https://localhost:7049',
  );

  static const String authority = String.fromEnvironment(
    'TB_AUTHORITY',
    defaultValue: 'https://localhost:8080/realms/transbrain',
  );

  static const String clientId = String.fromEnvironment(
    'TB_CLIENT_ID',
    defaultValue: 'transbrain-mobile',
  );

  /// Custom-scheme redirect used on iOS/Android. Must stay in sync with the
  /// `transbrain-mobile` client in `src/TransBrain.AppHost/realms/` and the
  /// intent filter / URL type registered in the platform projects.
  static const String mobileRedirectUri = String.fromEnvironment(
    'TB_MOBILE_REDIRECT_URI',
    defaultValue: 'de.transbrain.driver://callback',
  );
}
