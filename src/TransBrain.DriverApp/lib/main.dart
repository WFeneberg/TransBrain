import 'dart:async';

import 'package:material_ui/material_ui.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app.dart';
import 'core/auth/auth_service.dart';
import 'core/auth/oidc_client.dart';
import 'core/config/app_config.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('de_DE');
  final SharedPreferences preferences = await SharedPreferences.getInstance();
  final AuthService auth = AuthService(
    oidc: OidcClient(
      authority: AppConfig.authority,
      clientId: AppConfig.clientId,
    ),
    preferences: preferences,
  );
  runApp(TransBrainDriverApp(auth: auth));
  // After runApp so the splash shows while a redirect callback or refresh
  // token is being exchanged.
  unawaited(auth.initialize());
}
