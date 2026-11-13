import 'package:material_ui/material_ui.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../core/l10n/formats.dart';
import '../../core/models/tour_route.dart';
import '../shell/app_scope.dart';

/// The planned route on a map plus the day's timeline: legs, breaks,
/// loading/unloading and any driving-time rule findings.
class TourRouteScreen extends StatefulWidget {
  const TourRouteScreen({super.key, required this.tourId, required this.title});

  final String tourId;
  final String title;

  @override
  State<TourRouteScreen> createState() => _TourRouteScreenState();
}

class _TourRouteScreenState extends State<TourRouteScreen> {
  TourRoute? _route;
  Object? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_route == null && _error == null) {
      _load();
    }
  }

  Future<void> _load() async {
    final AppScope scope = AppScope.of(context);
    setState(() => _error = null);
    try {
      final TourRoute route = await scope.repository.getRoute(widget.tourId);
      if (mounted) {
        setState(() => _route = route);
      }
    } on Exception catch (error) {
      if (mounted) {
        setState(() => _error = error);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Route · ${widget.title}')),
      body: _error != null
          ? _buildError()
          : _route == null
              ? const Center(child: CircularProgressIndicator())
              : LayoutBuilder(
                  builder: (BuildContext context, BoxConstraints constraints) {
                    final bool wide = constraints.maxWidth >= 840;
                    return wide
                        ? _buildWide(_route!)
                        : _buildNarrow(_route!);
                  },
                ),
    );
  }

  Widget _buildError() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            const Icon(Icons.wrong_location_outlined,
                size: 56, color: Colors.grey),
            const SizedBox(height: 12),
            Text(
              'Route konnte nicht geladen werden.\n$_error',
              textAlign: TextAlign.center,
            ),
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

  Widget _buildNarrow(TourRoute route) {
    return Column(
      children: <Widget>[
        _SummaryBar(route: route),
        SizedBox(height: 300, child: _RouteMap(route: route)),
        Expanded(child: _TimelineList(route: route)),
      ],
    );
  }

  Widget _buildWide(TourRoute route) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Expanded(child: _RouteMap(route: route)),
        SizedBox(
          width: 400,
          child: Column(
            children: <Widget>[
              _SummaryBar(route: route),
              Expanded(child: _TimelineList(route: route)),
            ],
          ),
        ),
      ],
    );
  }
}

class _SummaryBar extends StatelessWidget {
  const _SummaryBar({required this.route});

  final TourRoute route;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    Widget item(IconData icon, String label, String value) => Expanded(
          child: Column(
            children: <Widget>[
              Icon(icon, size: 20, color: theme.colorScheme.primary),
              const SizedBox(height: 2),
              Text(
                value,
                style: theme.textTheme.titleSmall
                    ?.copyWith(fontWeight: FontWeight.bold),
                textAlign: TextAlign.center,
              ),
              Text(
                label,
                style: theme.textTheme.bodySmall
                    ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
          ),
        );
    return Padding(
      padding: const EdgeInsets.all(12),
      child: Row(
        children: <Widget>[
          item(Icons.route, 'Strecke', Formats.km(route.distanceMeters)),
          item(
            Icons.timer_outlined,
            'Lenkzeit',
            Formats.duration(
              Duration(seconds: route.drivingSeconds.round()),
            ),
          ),
          item(
            Icons.schedule,
            'Zeitfenster',
            Formats.timeRange(route.departure, route.end),
          ),
        ],
      ),
    );
  }
}

/// Web Mercator without the endlessly repeated world copies. With repetition on,
/// flutter_map 8.3.2 throws while painting the route line ("Infinite loop going
/// beyond 30 for world width"), which drops the line and breaks pointer input
/// for the whole screen. A tour map never spans the antimeridian anyway.
class _SingleWorldMercator extends Epsg3857 {
  const _SingleWorldMercator();

  @override
  bool get replicatesWorldLongitude => false;
}

class _RouteMap extends StatelessWidget {
  const _RouteMap({required this.route});

  final TourRoute route;

  @override
  Widget build(BuildContext context) {
    final List<LatLng> line = route.geometry
        .map((GeoPoint p) => LatLng(p.latitude, p.longitude))
        .toList();
    final List<LatLng> stopPoints = route.stops
        .map((RouteStop s) => LatLng(s.latitude, s.longitude))
        .toList();
    final List<LatLng> boundsSource =
        line.isNotEmpty ? line : stopPoints;
    if (boundsSource.isEmpty) {
      return const Center(child: Text('Keine Geodaten vorhanden.'));
    }
    return FlutterMap(
      options: MapOptions(
        crs: const _SingleWorldMercator(),
        initialCameraFit: CameraFit.bounds(
          bounds: LatLngBounds.fromPoints(boundsSource),
          padding: const EdgeInsets.all(48),
        ),
      ),
      children: <Widget>[
        TileLayer(
          urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
          userAgentPackageName: 'de.transbrain.driver',
        ),
        if (line.isNotEmpty)
          PolylineLayer(
            polylines: <Polyline<Object>>[
              Polyline<Object>(
                points: line,
                strokeWidth: 4,
                color: Colors.blue.shade700,
              ),
            ],
          ),
        MarkerLayer(
          markers: <Marker>[
            for (final RouteStop stop in route.stops)
              Marker(
                point: LatLng(stop.latitude, stop.longitude),
                width: 30,
                height: 30,
                child: Tooltip(
                  message:
                      '${stop.sequence}. ${stop.place} (${Labels.stopType(stop.stopType)})',
                  child: Container(
                    decoration: BoxDecoration(
                      color: stop.isPickup
                          ? Colors.green.shade700
                          : Colors.blue.shade800,
                      shape: BoxShape.circle,
                      border: Border.all(color: Colors.white, width: 2),
                    ),
                    alignment: Alignment.center,
                    child: Text(
                      '${stop.sequence}',
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 13,
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
        const SimpleAttributionWidget(
          source: Text('© OpenStreetMap-Mitwirkende'),
        ),
      ],
    );
  }
}

class _TimelineList extends StatelessWidget {
  const _TimelineList({required this.route});

  final TourRoute route;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    return ListView(
      padding: const EdgeInsets.only(bottom: 24),
      children: <Widget>[
        if (route.violations.isNotEmpty) ...<Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
            child: Text(
              'Hinweise zur Lenkzeit',
              style: theme.textTheme.titleSmall
                  ?.copyWith(fontWeight: FontWeight.bold),
            ),
          ),
          for (final RouteViolation violation in route.violations)
            ListTile(
              leading: Icon(
                violation.isViolation
                    ? Icons.error_outline
                    : Icons.warning_amber_outlined,
                color: violation.isViolation ? Colors.red : Colors.amber[800],
              ),
              title: Text(violation.message),
              subtitle: Text(Labels.violationSeverity(violation.severity)),
              dense: true,
            ),
          const Divider(),
        ],
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: Text(
            'Stopps',
            style: theme.textTheme.titleSmall
                ?.copyWith(fontWeight: FontWeight.bold),
          ),
        ),
        for (final RouteStop stop in route.stops)
          ListTile(
            leading: CircleAvatar(
              radius: 14,
              backgroundColor: stop.isPickup
                  ? Colors.green.shade700
                  : Colors.blue.shade800,
              child: Text(
                '${stop.sequence}',
                style: const TextStyle(color: Colors.white, fontSize: 12),
              ),
            ),
            title: Text(stop.place),
            subtitle: Text(
              '${stop.orderNumber} · ${Labels.stopType(stop.stopType)}',
            ),
            dense: true,
          ),
        const Divider(),
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: Text(
            'Tagesablauf',
            style: theme.textTheme.titleSmall
                ?.copyWith(fontWeight: FontWeight.bold),
          ),
        ),
        for (final ScheduleEntry entry in route.schedule)
          ListTile(
            leading: Icon(
              switch (entry.kind) {
                'Driving' => Icons.directions,
                'Break' => Icons.free_breakfast_outlined,
                _ => Icons.swap_vert,
              },
              color: theme.colorScheme.primary,
            ),
            title: Text(Labels.scheduleKind(entry.kind)),
            subtitle: Text(Formats.timeRange(entry.start, entry.end)),
            trailing: Text(Formats.duration(entry.duration)),
            dense: true,
          ),
        Padding(
          padding: const EdgeInsets.all(16),
          child: Text(
            'Frühester nächster Tourstart: '
            '${Formats.shortDate.format(route.earliestNextStart.toLocal())}, '
            '${Formats.time.format(route.earliestNextStart.toLocal())} Uhr',
            style: theme.textTheme.bodySmall
                ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
        ),
      ],
    );
  }
}
