import 'package:flutter_test/flutter_test.dart';
import 'package:transbrain_driver/core/auth/pkce.dart';

void main() {
  group('Pkce', () {
    test('challengeS256_rfc7636Vector_matchesExpectedChallenge', () {
      // Appendix B of RFC 7636.
      const String verifier = 'dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk';
      expect(
        Pkce.challengeS256(verifier),
        'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM',
      );
    });

    test('randomVerifier_defaultLength_usesOnlyUnreservedCharacters', () {
      final String verifier = Pkce.randomVerifier();
      expect(verifier.length, 64);
      expect(RegExp(r'^[A-Za-z0-9\-._~]+$').hasMatch(verifier), isTrue);
    });

    test('randomVerifier_twoCalls_differ', () {
      expect(Pkce.randomVerifier(), isNot(Pkce.randomVerifier()));
    });
  });
}
