import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_service.dart';
import '../../core/data/driver_app_repository.dart';

/// Hands the auth service and the active repository to the widget tree.
/// Rebuilds dependents when the session changes (sign-in, sign-out, demo).
class AppScope extends InheritedNotifier<AuthService> {
  const AppScope({
    super.key,
    required AuthService auth,
    required this.repositoryResolver,
    required super.child,
  }) : super(notifier: auth);

  final DriverAppRepository Function() repositoryResolver;

  AuthService get auth => notifier!;
  DriverAppRepository get repository => repositoryResolver();

  static AppScope of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<AppScope>()!;
}
