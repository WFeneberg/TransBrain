import 'package:material_ui/material_ui.dart';

import '../../core/l10n/formats.dart';
import '../../core/models/tour.dart';
import '../shell/app_scope.dart';
import 'tour_route_screen.dart';
import 'tour_widgets.dart';

/// One tour: load, stops in driving order, and the status actions a driver
/// may perform — start and complete.
class TourDetailScreen extends StatefulWidget {
  const TourDetailScreen({super.key, required this.tourId});

  final String tourId;

  @override
  State<TourDetailScreen> createState() => _TourDetailScreenState();
}

class _TourDetailScreenState extends State<TourDetailScreen> {
  Tour? _tour;
  Object? _error;
  bool _working = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_tour == null && _error == null) {
      _load();
    }
  }

  Future<void> _load() async {
    final AppScope scope = AppScope.of(context);
    setState(() => _error = null);
    try {
      final Tour tour = await scope.repository.getTour(widget.tourId);
      if (mounted) {
        setState(() => _tour = tour);
      }
    } on Exception catch (error) {
      if (mounted) {
        setState(() => _error = error);
      }
    }
  }

  Future<void> _changeStatus({required bool start}) async {
    final AppScope scope = AppScope.of(context);
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: Text(start ? 'Tour starten?' : 'Tour abschließen?'),
        content: Text(
          start
              ? 'Die Tour wird als „Unterwegs“ gemeldet und alle Aufträge '
                  'gehen in den Transport.'
              : 'Die Tour wird als „Abgeschlossen“ gemeldet und alle '
                  'Aufträge gelten als zugestellt.',
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Abbrechen'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(start ? 'Starten' : 'Abschließen'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
      return;
    }
    setState(() => _working = true);
    try {
      final Tour updated = start
          ? await scope.repository.startTour(widget.tourId)
          : await scope.repository.completeTour(widget.tourId);
      if (!mounted) {
        return;
      }
      setState(() {
        _tour = updated;
        _working = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            start ? 'Tour gestartet. Gute Fahrt!' : 'Tour abgeschlossen.',
          ),
        ),
      );
    } on Exception catch (error) {
      if (!mounted) {
        return;
      }
      setState(() => _working = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('$error'),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
    }
  }

  void _openRoute() {
    final Tour? tour = _tour;
    if (tour == null) {
      return;
    }
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => TourRouteScreen(
          tourId: tour.id,
          title: Formats.shortDate.format(tour.tourDate),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final Tour? tour = _tour;
    return Scaffold(
      appBar: AppBar(title: const Text('Tourdetails')),
      body: _error != null
          ? _buildError()
          : tour == null
              ? const Center(child: CircularProgressIndicator())
              : _buildDetail(tour),
      bottomNavigationBar: tour == null ? null : _buildActionBar(tour),
    );
  }

  Widget _buildError() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            const Icon(Icons.cloud_off, size: 56, color: Colors.grey),
            const SizedBox(height: 12),
            Text('$_error', textAlign: TextAlign.center),
            const SizedBox(height: 16),
            FilledButton.tonal(
              onPressed: _load,
              child: const Text('Erneut versuchen'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDetail(Tour tour) {
    final ThemeData theme = Theme.of(context);
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(16),
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  Formats.weekdayDate.format(tour.tourDate),
                  style: theme.textTheme.titleLarge
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
              ),
              TourStatusChip(status: tour.status),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            '${tour.vehicleLicensePlate} · ${tour.driverName}',
            style: theme.textTheme.bodyLarge
                ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Beladung',
                    style: theme.textTheme.titleMedium
                        ?.copyWith(fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 12),
                  CapacityBar(
                    label: 'Gewicht',
                    usedText:
                        '${Formats.weightKg(tour.totalWeightKg)} / ${Formats.weightKg(tour.vehiclePayloadKg)}',
                    ratio: tour.vehiclePayloadKg == 0
                        ? 0
                        : tour.totalWeightKg / tour.vehiclePayloadKg,
                  ),
                  const SizedBox(height: 12),
                  CapacityBar(
                    label: 'Lademeter',
                    usedText:
                        '${Formats.loadMeters(tour.totalLoadMeters)} / ${Formats.loadMeters(tour.vehicleLoadMeters)}',
                    ratio: tour.vehicleLoadMeters == 0
                        ? 0
                        : tour.totalLoadMeters / tour.vehicleLoadMeters,
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 8),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                    child: Text(
                      'Stopps (${tour.stops.length})',
                      style: theme.textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.w600),
                    ),
                  ),
                  if (tour.stops.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(16),
                      child: Text('Dieser Tour sind keine Stopps zugeordnet.'),
                    )
                  else
                    for (final TourStop stop in tour.stops)
                      ListTile(
                        leading: StopAvatar(
                          sequence: stop.sequence,
                          isPickup: stop.isPickup,
                        ),
                        title: Text(stop.orderNumber),
                        subtitle: Text(Labels.stopType(stop.stopType)),
                        trailing: Icon(
                          stop.isPickup ? Icons.upload : Icons.download,
                          color: stop.isPickup
                              ? Colors.green.shade700
                              : Colors.blue.shade700,
                        ),
                        dense: true,
                      ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: tour.stops.isEmpty ? null : _openRoute,
            icon: const Icon(Icons.map_outlined),
            label: const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Text('Route & Karte anzeigen'),
            ),
          ),
          const SizedBox(height: 80),
        ],
      ),
    );
  }

  Widget? _buildActionBar(Tour tour) {
    if (tour.isCompleted) {
      return null;
    }
    final bool start = tour.isPlanned;
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
        child: FilledButton.icon(
          style: start
              ? null
              : FilledButton.styleFrom(
                  backgroundColor: Colors.green.shade700,
                  foregroundColor: Colors.white,
                ),
          onPressed: _working ? null : () => _changeStatus(start: start),
          icon: _working
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : Icon(start ? Icons.play_arrow : Icons.flag),
          label: Padding(
            padding: const EdgeInsets.symmetric(vertical: 12),
            child: Text(start ? 'Tour starten' : 'Tour abschließen'),
          ),
        ),
      ),
    );
  }
}
