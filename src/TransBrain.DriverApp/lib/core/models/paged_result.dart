/// One page of a paged API listing.
class PagedResult<T> {
  PagedResult({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
  });

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalCount;

  bool get hasMore => page * pageSize < totalCount;

  factory PagedResult.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) itemFromJson,
  ) =>
      PagedResult<T>(
        items: (json['items'] as List<dynamic>? ?? const <dynamic>[])
            .map((dynamic i) => itemFromJson(i as Map<String, dynamic>))
            .toList(),
        page: (json['page'] as num).toInt(),
        pageSize: (json['pageSize'] as num).toInt(),
        totalCount: (json['totalCount'] as num).toInt(),
      );
}
