import 'package:material_ui/material_ui.dart';

import '../profile/profile_screen.dart';
import '../tours/tour_list_screen.dart';
import 'app_scope.dart';

/// Signed-in frame: bottom navigation between tours and profile, plus the
/// demo-mode banner.
class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    final AppScope scope = AppScope.of(context);
    return Scaffold(
      body: Column(
        children: <Widget>[
          if (scope.auth.isDemo) const _DemoBanner(),
          Expanded(
            child: IndexedStack(
              index: _index,
              children: const <Widget>[
                TourListScreen(),
                ProfileScreen(),
              ],
            ),
          ),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (int value) => setState(() => _index = value),
        destinations: const <Widget>[
          NavigationDestination(
            icon: Icon(Icons.local_shipping_outlined),
            selectedIcon: Icon(Icons.local_shipping),
            label: 'Touren',
          ),
          NavigationDestination(
            icon: Icon(Icons.person_outline),
            selectedIcon: Icon(Icons.person),
            label: 'Profil',
          ),
        ],
      ),
    );
  }
}

class _DemoBanner extends StatelessWidget {
  const _DemoBanner();

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    return Material(
      color: theme.colorScheme.tertiaryContainer,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
          child: Row(
            children: <Widget>[
              Icon(
                Icons.science_outlined,
                size: 18,
                color: theme.colorScheme.onTertiaryContainer,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Demo-Modus: Beispieldaten, keine Verbindung zum Backend.',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.onTertiaryContainer,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
