import 'package:material_ui/material_ui.dart';

import '../../core/l10n/formats.dart';

/// Colored status chip used on cards and detail headers.
class TourStatusChip extends StatelessWidget {
  const TourStatusChip({super.key, required this.status});

  final String status;

  @override
  Widget build(BuildContext context) {
    final (Color background, Color foreground, IconData icon) =
        switch (status) {
      'Planned' => (
          Colors.blue.shade50,
          Colors.blue.shade800,
          Icons.schedule,
        ),
      'InProgress' => (
          Colors.orange.shade50,
          Colors.orange.shade800,
          Icons.local_shipping,
        ),
      'Completed' => (
          Colors.green.shade50,
          Colors.green.shade800,
          Icons.check_circle,
        ),
      _ => (Colors.grey.shade200, Colors.grey.shade800, Icons.help_outline),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: 16, color: foreground),
          const SizedBox(width: 4),
          Text(
            Labels.tourStatus(status),
            style: TextStyle(
              color: foreground,
              fontWeight: FontWeight.w600,
              fontSize: 12,
            ),
          ),
        ],
      ),
    );
  }
}

/// A labelled utilization bar, e.g. weight against the vehicle's payload.
class CapacityBar extends StatelessWidget {
  const CapacityBar({
    super.key,
    required this.label,
    required this.usedText,
    required this.ratio,
  });

  final String label;
  final String usedText;
  final double ratio;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final Color color = ratio > 1.0
        ? theme.colorScheme.error
        : ratio > 0.9
            ? Colors.orange
            : theme.colorScheme.primary;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: <Widget>[
            Text(label, style: theme.textTheme.bodyMedium),
            Text(
              usedText,
              style: theme.textTheme.bodyMedium
                  ?.copyWith(fontWeight: FontWeight.w600),
            ),
          ],
        ),
        const SizedBox(height: 4),
        ClipRRect(
          borderRadius: BorderRadius.circular(4),
          child: LinearProgressIndicator(
            value: ratio.clamp(0.0, 1.0),
            minHeight: 8,
            color: color,
            backgroundColor: theme.colorScheme.surfaceContainerHighest,
          ),
        ),
      ],
    );
  }
}

/// Icon + color conventions for pickup/delivery stops.
class StopAvatar extends StatelessWidget {
  const StopAvatar({super.key, required this.sequence, required this.isPickup});

  final int sequence;
  final bool isPickup;

  @override
  Widget build(BuildContext context) {
    final Color color = isPickup ? Colors.green.shade700 : Colors.blue.shade700;
    return CircleAvatar(
      radius: 16,
      backgroundColor: color.withValues(alpha: 0.15),
      child: Text(
        '$sequence',
        style: TextStyle(
          color: color,
          fontWeight: FontWeight.bold,
          fontSize: 14,
        ),
      ),
    );
  }
}
