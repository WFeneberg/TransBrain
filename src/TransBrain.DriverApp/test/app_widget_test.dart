import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:transbrain_driver/app.dart';
import 'package:transbrain_driver/core/auth/auth_service.dart';
import 'package:transbrain_driver/core/auth/oidc_client.dart';

import 'helpers.dart';

Future<AuthService> freshAuth() async {
  SharedPreferences.setMockInitialValues(<String, Object>{});
  final SharedPreferences prefs = await SharedPreferences.getInstance();
  final AuthService auth = AuthService(
    oidc: OidcClient(
      authority: 'https://kc/realms/transbrain',
      clientId: 'transbrain-mobile',
    ),
    preferences: prefs,
    flowDelegate: FakeAuthFlowDelegate(),
  );
  await auth.initialize();
  return auth;
}

void main() {
  setUpAll(() async {
    await initializeDateFormatting('de_DE');
  });

  testWidgets('app_start_showsLoginWithDemoOption',
      (WidgetTester tester) async {
    final AuthService auth = await freshAuth();
    await tester.pumpWidget(TransBrainDriverApp(auth: auth));
    await tester.pump();

    expect(find.text('TransBrain Fahrer'), findsOneWidget);
    expect(find.text('Mit TransBrain-Konto anmelden'), findsOneWidget);
    expect(find.text('Demo-Modus (ohne Anmeldung)'), findsOneWidget);
  });

  testWidgets('demoMode_signIn_showsTourListWithDemoBanner',
      (WidgetTester tester) async {
    final AuthService auth = await freshAuth();
    await tester.pumpWidget(TransBrainDriverApp(auth: auth));
    await tester.pump();

    await tester.tap(find.text('Demo-Modus (ohne Anmeldung)'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pump();

    expect(find.text('Meine Touren'), findsOneWidget);
    expect(
      find.text('Demo-Modus: Beispieldaten, keine Verbindung zum Backend.'),
      findsOneWidget,
    );
    expect(find.text('LL-TB 204'), findsOneWidget);
    expect(find.text('LL-TB 101'), findsWidgets);
  });

  testWidgets('demoMode_openPlannedTourAndStart_showsConfirmationAndNewStatus',
      (WidgetTester tester) async {
    final AuthService auth = await freshAuth();
    await tester.pumpWidget(TransBrainDriverApp(auth: auth));
    await tester.pump();

    await tester.tap(find.text('Demo-Modus (ohne Anmeldung)'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pump();

    // The first LL-TB 101 entry is today's planned tour.
    await tester.tap(find.text('LL-TB 101').first);
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pump();

    expect(find.text('Tour starten'), findsOneWidget);

    await tester.tap(find.text('Tour starten'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.text('Tour starten?'), findsOneWidget);
    await tester.tap(find.text('Starten'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pump();

    expect(find.text('Tour gestartet. Gute Fahrt!'), findsOneWidget);
    expect(find.text('Tour abschließen'), findsOneWidget);

    // Let the snackbar expire so no timers stay pending.
    await tester.pump(const Duration(seconds: 5));
    await tester.pumpAndSettle();
  });

  testWidgets('demoMode_profileTab_showsDemoDriverAndSignOut',
      (WidgetTester tester) async {
    final AuthService auth = await freshAuth();
    await tester.pumpWidget(TransBrainDriverApp(auth: auth));
    await tester.pump();

    await tester.tap(find.text('Demo-Modus (ohne Anmeldung)'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pump();

    await tester.tap(find.text('Profil'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pump();

    expect(find.text('Max Mustermann'), findsWidgets);

    await tester.tap(find.text('Abmelden'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.text('Abmelden?'), findsOneWidget);
    // The dialog's confirm button is the last 'Abmelden' in the tree.
    await tester.tap(find.text('Abmelden').last);
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.text('Demo-Modus (ohne Anmeldung)'), findsOneWidget);
  });
}
