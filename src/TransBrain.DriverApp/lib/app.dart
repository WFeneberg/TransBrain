import 'package:cupertino_ui/cupertino_ui.dart'
    show GlobalCupertinoLocalizations;
import 'package:flutter_localizations/flutter_localizations.dart'
    show GlobalWidgetsLocalizations;
import 'package:material_ui/material_ui.dart';

import 'core/auth/auth_service.dart';
import 'core/config/app_config.dart';
import 'core/api/api_client.dart';
import 'core/data/demo_driver_app_repository.dart';
import 'core/data/driver_app_repository.dart';
import 'core/data/http_driver_app_repository.dart';
import 'features/login/login_screen.dart';
import 'features/shell/app_scope.dart';
import 'features/shell/home_shell.dart';

/// Root widget: theming, German localization, and the auth gate that switches
/// between login and the signed-in shell.
class TransBrainDriverApp extends StatefulWidget {
  const TransBrainDriverApp({super.key, required this.auth});

  final AuthService auth;

  @override
  State<TransBrainDriverApp> createState() => _TransBrainDriverAppState();
}

class _TransBrainDriverAppState extends State<TransBrainDriverApp> {
  HttpDriverAppRepository? _httpRepository;
  DemoDriverAppRepository? _demoRepository;
  bool _wasDemo = false;

  DriverAppRepository _resolveRepository() {
    if (widget.auth.isDemo) {
      return _demoRepository ??= DemoDriverAppRepository();
    }
    return _httpRepository ??= HttpDriverAppRepository(
      ApiClient(baseUrl: AppConfig.apiBaseUrl, auth: widget.auth),
    );
  }

  @override
  void initState() {
    super.initState();
    widget.auth.addListener(_onAuthChanged);
  }

  void _onAuthChanged() {
    // A fresh demo session starts with fresh demo data.
    if (!widget.auth.isDemo && _wasDemo) {
      _demoRepository = null;
    }
    _wasDemo = widget.auth.isDemo;
  }

  @override
  void dispose() {
    widget.auth.removeListener(_onAuthChanged);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    const Color seed = Color(0xFF1565C0);
    return AppScope(
      auth: widget.auth,
      repositoryResolver: _resolveRepository,
      child: MaterialApp(
        title: 'TransBrain Fahrer',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          colorScheme: ColorScheme.fromSeed(seedColor: seed),
          useMaterial3: true,
        ),
        darkTheme: ThemeData(
          colorScheme: ColorScheme.fromSeed(
            seedColor: seed,
            brightness: Brightness.dark,
          ),
          useMaterial3: true,
        ),
        locale: const Locale('de'),
        supportedLocales: const <Locale>[Locale('de'), Locale('en')],
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: _AuthGate(auth: widget.auth),
      ),
    );
  }
}

class _AuthGate extends StatelessWidget {
  const _AuthGate({required this.auth});

  final AuthService auth;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: auth,
      builder: (BuildContext context, _) {
        if (!auth.isInitialized) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }
        if (!auth.isAuthenticated) {
          return LoginScreen(auth: auth);
        }
        return const HomeShell();
      },
    );
  }
}
