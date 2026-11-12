import 'package:material_ui/material_ui.dart';

import '../../core/auth/auth_session.dart';
import '../../core/l10n/formats.dart';
import '../../core/models/driver.dart';
import '../shell/app_scope.dart';

/// Account and driver master data of the signed-in user, plus sign-out.
class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  Driver? _driver;
  bool _loaded = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_loaded) {
      _load();
    }
  }

  Future<void> _load() async {
    final AppScope scope = AppScope.of(context);
    try {
      final Driver? driver = await scope.repository
          .findMyDriver(scope.auth.session?.userId);
      if (mounted) {
        setState(() {
          _driver = driver;
          _loaded = true;
        });
      }
    } on Exception {
      if (mounted) {
        setState(() => _loaded = true);
      }
    }
  }

  Future<void> _signOut() async {
    final AppScope scope = AppScope.of(context);
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: const Text('Abmelden?'),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Abbrechen'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Abmelden'),
          ),
        ],
      ),
    );
    if (confirmed == true) {
      await scope.auth.signOut();
    }
  }

  @override
  Widget build(BuildContext context) {
    final AppScope scope = AppScope.of(context);
    final AuthSession? session = scope.auth.session;
    final ThemeData theme = Theme.of(context);
    final Driver? driver = _driver;
    final String displayName = driver?.fullName ?? session?.userName ?? '–';
    final String initials = displayName
        .split(RegExp(r'[\s.]+'))
        .where((String part) => part.isNotEmpty)
        .take(2)
        .map((String part) => part[0].toUpperCase())
        .join();

    return Scaffold(
      appBar: AppBar(title: const Text('Profil')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: <Widget>[
          Center(
            child: Column(
              children: <Widget>[
                CircleAvatar(
                  radius: 36,
                  backgroundColor: theme.colorScheme.primaryContainer,
                  child: Text(
                    initials,
                    style: theme.textTheme.headlineSmall?.copyWith(
                      color: theme.colorScheme.onPrimaryContainer,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                Text(
                  displayName,
                  style: theme.textTheme.titleLarge
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
                Text(
                  session?.userName ?? '',
                  style: theme.textTheme.bodyMedium
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  children: <Widget>[
                    for (final String role in session?.roles ??
                        const <String>[])
                      if (const <String>['admin', 'disponent', 'fahrer', 'viewer']
                          .contains(role))
                        Chip(
                          label: Text(role),
                          visualDensity: VisualDensity.compact,
                        ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          if (!_loaded)
            const Center(
              child: Padding(
                padding: EdgeInsets.all(16),
                child: CircularProgressIndicator(),
              ),
            )
          else if (driver != null)
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      'Fahrerprofil',
                      style: theme.textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 8),
                    _InfoRow(
                      icon: Icons.badge_outlined,
                      label: 'Führerscheinklassen',
                      value: driver.licenseClasses.join(', '),
                    ),
                    _InfoRow(
                      icon: Icons.event_outlined,
                      label: 'Führerschein gültig bis',
                      value: Formats.shortDate.format(driver.licenseValidUntil),
                      warn: driver.licenseValidUntil
                          .isBefore(DateTime.now().add(const Duration(days: 90))),
                    ),
                    _InfoRow(
                      icon: Icons.verified_user_outlined,
                      label: 'Status',
                      value: Labels.driverStatus(driver.status),
                    ),
                  ],
                ),
              ),
            )
          else
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Row(
                  children: <Widget>[
                    Icon(Icons.link_off, color: theme.colorScheme.outline),
                    const SizedBox(width: 12),
                    const Expanded(
                      child: Text(
                        'Mit diesem Konto ist kein Fahrerprofil verknüpft. '
                        'Bitte an die Disposition wenden.',
                      ),
                    ),
                  ],
                ),
              ),
            ),
          const SizedBox(height: 24),
          FilledButton.tonal(
            style: FilledButton.styleFrom(
              backgroundColor: theme.colorScheme.errorContainer,
              foregroundColor: theme.colorScheme.onErrorContainer,
            ),
            onPressed: _signOut,
            child: const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Text('Abmelden'),
            ),
          ),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.icon,
    required this.label,
    required this.value,
    this.warn = false,
  });

  final IconData icon;
  final String label;
  final String value;
  final bool warn;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: <Widget>[
          Icon(icon, size: 20, color: theme.colorScheme.onSurfaceVariant),
          const SizedBox(width: 12),
          Expanded(child: Text(label)),
          Text(
            value,
            style: theme.textTheme.bodyMedium?.copyWith(
              fontWeight: FontWeight.w600,
              color: warn ? Colors.orange.shade800 : null,
            ),
          ),
          if (warn) ...<Widget>[
            const SizedBox(width: 4),
            Icon(Icons.warning_amber, size: 16, color: Colors.orange.shade800),
          ],
        ],
      ),
    );
  }
}
