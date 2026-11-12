import 'package:flutter_test/flutter_test.dart';
import 'package:transbrain_driver/core/auth/auth_session.dart';

import 'helpers.dart';

void main() {
  group('AuthSession', () {
    test('fromTokenResponse_keycloakShape_mapsClaimsAndRoles', () {
      final String accessToken = makeJwt(<String, dynamic>{
        'sub': 'user-123',
        'preferred_username': 'fahrer.user',
        'realm_access': <String, dynamic>{
          'roles': <String>['fahrer', 'offline_access'],
        },
      });
      final AuthSession session =
          AuthSession.fromTokenResponse(<String, dynamic>{
        'access_token': accessToken,
        'refresh_token': 'refresh-1',
        'expires_in': 300,
      });

      expect(session.userId, 'user-123');
      expect(session.userName, 'fahrer.user');
      expect(session.roles, contains('fahrer'));
      expect(session.refreshToken, 'refresh-1');
      expect(session.isDemo, isFalse);
      expect(session.needsRefresh, isFalse);
    });

    test('fromTokenResponse_shortExpiry_needsRefresh', () {
      final AuthSession session =
          AuthSession.fromTokenResponse(<String, dynamic>{
        'access_token': makeJwt(<String, dynamic>{'sub': 'u'}),
        'expires_in': 5,
      });
      expect(session.needsRefresh, isTrue);
    });

    test('demo_always_carriesDriverRoleAndNeverExpires', () {
      final AuthSession session = AuthSession.demo();
      expect(session.isDemo, isTrue);
      expect(session.hasRole('fahrer'), isTrue);
      expect(session.needsRefresh, isFalse);
    });

    test('decodeJwtPayload_notAJwt_throwsFormatException', () {
      expect(() => decodeJwtPayload('garbage'), throwsFormatException);
    });
  });
}
