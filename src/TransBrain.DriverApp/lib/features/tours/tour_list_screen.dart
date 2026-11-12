import 'package:material_ui/material_ui.dart';

import '../../core/l10n/formats.dart';
import '../../core/models/tour.dart';
import '../shell/app_scope.dart';
import 'tour_detail_screen.dart';
import 'tour_widgets.dart';

/// The driver's tours, grouped by day with a status filter and pull-to-refresh.
class TourListScreen extends StatefulWidget {
  const TourListScreen({super.key});

  @override
  State<TourListScreen> createState() => _TourListScreenState();
}

class _TourListScreenState extends State<TourListScreen> {
  List<Tour>? _tours;
  Object? _error;
  String? _statusFilter;
  bool _loading = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_tours == null && _error == null && !_loading) {
      _load();
    }
  }

  Future<void> _load() async {
    final AppScope scope = AppScope.of(context);
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final List<Tour> tours = await scope.repository.listMyTours();
      if (!mounted) {
        return;
      }
      setState(() {
        _tours = tours;
        _loading = false;
      });
    } on Exception catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = error;
        _loading = false;
      });
    }
  }

  Future<void> _openTour(Tour tour) async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => TourDetailScreen(tourId: tour.id),
      ),
    );
    // Status may have changed on the detail screen.
    await _load();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Meine Touren'),
        actions: <Widget>[
          IconButton(
            tooltip: 'Aktualisieren',
            onPressed: _loading ? null : _load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
            child: SegmentedButton<String?>(
              segments: const <ButtonSegment<String?>>[
                ButtonSegment<String?>(value: null, label: Text('Alle')),
                ButtonSegment<String?>(
                  value: 'Planned',
                  label: Text('Geplant'),
                ),
                ButtonSegment<String?>(
                  value: 'InProgress',
                  label: Text('Unterwegs'),
                ),
                ButtonSegment<String?>(
                  value: 'Completed',
                  label: Text('Fertig'),
                ),
              ],
              selected: <String?>{_statusFilter},
              onSelectionChanged: (Set<String?> selection) =>
                  setState(() => _statusFilter = selection.first),
              showSelectedIcon: false,
            ),
          ),
          Expanded(child: _buildBody()),
        ],
      ),
    );
  }

  Widget _buildBody() {
    if (_loading && _tours == null) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return _ErrorView(error: _error!, onRetry: _load);
    }
    final List<Tour> filtered = (_tours ?? const <Tour>[])
        .where((Tour t) => _statusFilter == null || t.status == _statusFilter)
        .toList();
    if (filtered.isEmpty) {
      return RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: const <Widget>[
            SizedBox(height: 120),
            Icon(Icons.inbox_outlined, size: 56, color: Colors.grey),
            SizedBox(height: 12),
            Center(child: Text('Keine Touren gefunden.')),
          ],
        ),
      );
    }
    final List<_Section> sections = _groupByDay(filtered);
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.only(bottom: 24),
        itemCount: sections.fold<int>(
          0,
          (int sum, _Section s) => sum + 1 + s.tours.length,
        ),
        itemBuilder: (BuildContext context, int index) {
          int cursor = index;
          for (final _Section section in sections) {
            if (cursor == 0) {
              return _SectionHeader(title: section.title);
            }
            cursor--;
            if (cursor < section.tours.length) {
              final Tour tour = section.tours[cursor];
              return _TourCard(tour: tour, onTap: () => _openTour(tour));
            }
            cursor -= section.tours.length;
          }
          return const SizedBox.shrink();
        },
      ),
    );
  }

  List<_Section> _groupByDay(List<Tour> tours) {
    final DateTime now = DateTime.now();
    final DateTime today = DateTime(now.year, now.month, now.day);
    bool sameDay(DateTime a, DateTime b) =>
        a.year == b.year && a.month == b.month && a.day == b.day;

    int statusRank(Tour t) => t.isInProgress
        ? 0
        : t.isPlanned
            ? 1
            : 2;

    final List<Tour> todays = tours
        .where((Tour t) => sameDay(t.tourDate, today))
        .toList()
      ..sort((Tour a, Tour b) => statusRank(a).compareTo(statusRank(b)));
    final List<Tour> upcoming = tours
        .where((Tour t) => t.tourDate.isAfter(today) &&
            !sameDay(t.tourDate, today))
        .toList()
      ..sort((Tour a, Tour b) => a.tourDate.compareTo(b.tourDate));
    final List<Tour> past = tours
        .where((Tour t) => t.tourDate.isBefore(today) &&
            !sameDay(t.tourDate, today))
        .toList()
      ..sort((Tour a, Tour b) => b.tourDate.compareTo(a.tourDate));

    return <_Section>[
      if (todays.isNotEmpty) _Section('Heute', todays),
      if (upcoming.isNotEmpty) _Section('Kommende Touren', upcoming),
      if (past.isNotEmpty) _Section('Vergangene Touren', past),
    ];
  }
}

class _Section {
  _Section(this.title, this.tours);

  final String title;
  final List<Tour> tours;
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      child: Text(
        title,
        style: Theme.of(context).textTheme.titleSmall?.copyWith(
              color: Theme.of(context).colorScheme.primary,
              fontWeight: FontWeight.bold,
            ),
      ),
    );
  }
}

class _TourCard extends StatelessWidget {
  const _TourCard({required this.tour, required this.onTap});

  final Tour tour;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      Formats.weekdayDate.format(tour.tourDate),
                      style: theme.textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.w600),
                    ),
                  ),
                  TourStatusChip(status: tour.status),
                ],
              ),
              const SizedBox(height: 8),
              Row(
                children: <Widget>[
                  Icon(
                    Icons.local_shipping_outlined,
                    size: 18,
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                  const SizedBox(width: 6),
                  Text(
                    tour.vehicleLicensePlate,
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(width: 16),
                  Icon(
                    Icons.place_outlined,
                    size: 18,
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                  const SizedBox(width: 6),
                  Text('${tour.stops.length} Stopps'),
                  const SizedBox(width: 16),
                  Icon(
                    Icons.scale_outlined,
                    size: 18,
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                  const SizedBox(width: 6),
                  Flexible(child: Text(Formats.weightKg(tour.totalWeightKg))),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            const Icon(Icons.cloud_off, size: 56, color: Colors.grey),
            const SizedBox(height: 12),
            Text(
              'Touren konnten nicht geladen werden.\n$error',
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 16),
            FilledButton.tonal(
              onPressed: onRetry,
              child: const Text('Erneut versuchen'),
            ),
          ],
        ),
      ),
    );
  }
}
