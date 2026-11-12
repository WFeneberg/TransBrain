import 'dart:convert';
import 'dart:math';

import 'package:crypto/crypto.dart';

/// PKCE (RFC 7636) helpers for the authorization-code flow.
abstract final class Pkce {
  static const String _charset =
      'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~';

  /// A cryptographically random string from the RFC 7636 unreserved charset.
  static String randomVerifier([int length = 64]) {
    final Random random = Random.secure();
    return List<String>.generate(
      length,
      (_) => _charset[random.nextInt(_charset.length)],
    ).join();
  }

  /// `BASE64URL(SHA256(verifier))` without padding — the S256 challenge.
  static String challengeS256(String verifier) =>
      base64UrlEncode(sha256.convert(ascii.encode(verifier)).bytes)
          .replaceAll('=', '');

  /// Random `state` parameter tying the callback to the request that sent it.
  static String randomState() => randomVerifier(32);
}
