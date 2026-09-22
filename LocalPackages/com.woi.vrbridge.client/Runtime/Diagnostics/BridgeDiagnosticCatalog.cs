using System;
using System.Collections.Generic;
using Woi.VrBridge.Client.Protocol;

namespace Woi.VrBridge.Client.Diagnostics
{
    public static class BridgeDiagnosticCatalog
    {
        static readonly Dictionary<string, (string En, string Tr, string ActionEn, string ActionTr)> Messages =
            new Dictionary<string, (string, string, string, string)>(StringComparer.Ordinal)
            {
                [ErrorCodes.DiscoveryTimeout] = (
                    "Bridge not found.",
                    "Bridge bulunamadı.",
                    "Ensure the PC and Quest are on the same network.",
                    "PC ve Quest'in aynı ağda olduğunu kontrol edin."),
                [ErrorCodes.DiscoveryNonceMismatch] = (
                    "Invalid discovery offer.",
                    "Geçersiz keşif teklifi.",
                    "Retry connection.",
                    "Bağlantıyı yeniden deneyin."),
                [ErrorCodes.DiscoveryLoopbackRejected] = (
                    "Bridge found but returned a loopback address.",
                    "Bridge bulundu ancak döngü adresi döndü.",
                    "Check Bridge LAN binding and firewall settings.",
                    "Bridge LAN bağlama ve güvenlik duvarı ayarlarını kontrol edin."),
                [ErrorCodes.DiscoveryInvalidOffer] = (
                    "Invalid discovery offer.",
                    "Geçersiz keşif teklifi.",
                    "Retry connection.",
                    "Bağlantıyı yeniden deneyin."),
                [ErrorCodes.NoActiveNetworkAdapter] = (
                    "No active network.",
                    "Aktif ağ yok.",
                    "Connect Quest to Wi-Fi and retry.",
                    "Quest'i Wi-Fi'ye bağlayıp yeniden deneyin."),
                [ErrorCodes.WebSocketConnectFailed] = (
                    "Bridge found but connection could not be established.",
                    "Bridge bulundu ancak bağlantı kurulamadı.",
                    "Check the PC firewall and port 17881.",
                    "PC güvenlik duvarını ve 17881 portunu kontrol edin."),
                [ErrorCodes.DeviceConnectionFailed] = (
                    "Bridge found but connection could not be established.",
                    "Bridge bulundu ancak bağlantı kurulamadı.",
                    "Check the PC firewall and port 17881.",
                    "PC güvenlik duvarını ve 17881 portunu kontrol edin."),
                [ErrorCodes.DeviceAuthenticationFailed] = (
                    "Authentication failed.",
                    "Kimlik doğrulama başarısız.",
                    "Re-pair the device.",
                    "Cihazı yeniden eşleştirin."),
                [ErrorCodes.PairingCodeInvalid] = (
                    "Pairing failed.",
                    "Eşleştirme başarısız.",
                    "The code may have expired or already been used.",
                    "Kodun süresi dolmuş veya daha önce kullanılmış olabilir."),
                [ErrorCodes.PairingRequired] = (
                    "Pairing required.",
                    "Eşleştirme gerekli.",
                    "Enter the 6-digit code from the Operator Console.",
                    "Operatör ekranında oluşturulan 6 haneli kodu girin."),
                [ErrorCodes.SessionBusyRejected] = (
                    "Session command rejected.",
                    "Oturum komutu reddedildi.",
                    "Another active session is running on this device.",
                    "Cihazda başka bir aktif oturum bulunuyor."),
                [ErrorCodes.IdempotencyConflict] = (
                    "Conflicting session command.",
                    "Çakışan oturum komutu.",
                    "Cancel the conflicting session from Operator Console.",
                    "Çakışan oturumu Operatör Konsolu'ndan iptal edin."),
                [ErrorCodes.ParticipantValidationFailed] = (
                    "Invalid participant.",
                    "Geçersiz katılımcı.",
                    "Correct participant name and personnel ID, then retry.",
                    "Katılımcı adı ve personel kimliğini düzeltip yeniden deneyin."),
                [ErrorCodes.SessionCommandRejected] = (
                    "Module unavailable.",
                    "Modül kullanılamıyor.",
                    "Install or enable the requested module.",
                    "İstenen modülü yükleyin veya etkinleştirin."),
                [ErrorCodes.DeviceIdentityReadFailed] = (
                    "Device identity storage is corrupted.",
                    "Cihaz kimliği deposu bozulmuş.",
                    "Reset device identity only after confirmation.",
                    "Yalnızca onaydan sonra cihaz kimliğini sıfırlayın."),
                [ErrorCodes.HeartbeatTimedOut] = (
                    "Quest connection interrupted.",
                    "Quest bağlantısı kesildi.",
                    "Reconnecting…",
                    "Yeniden bağlanılıyor…"),
                [ErrorCodes.CredentialStoreUnavailable] = (
                    "Secure credential store unavailable.",
                    "Güvenli kimlik deposu kullanılamıyor.",
                    "Restart the application. If it persists, re-pair.",
                    "Uygulamayı yeniden başlatın. Devam ederse yeniden eşleştirin."),
                [ErrorCodes.TransportNotConnected] = (
                    "Not connected to Bridge.",
                    "Bridge'e bağlı değil.",
                    "Retry connection.",
                    "Bağlantıyı yeniden deneyin.")
            };

        public static string Localize(string errorCode, string locale, string fallback = null)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                return fallback ?? "Unknown error.";
            }

            if (!Messages.TryGetValue(errorCode, out var pair))
            {
                return fallback ?? errorCode;
            }

            return string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) ? pair.Tr : pair.En;
        }

        public static string SuggestedAction(string errorCode, string locale)
        {
            if (string.IsNullOrWhiteSpace(errorCode) || !Messages.TryGetValue(errorCode, out var pair))
            {
                return string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase)
                    ? "Bağlantıyı yeniden deneyin."
                    : "Retry connection.";
            }

            return string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) ? pair.ActionTr : pair.ActionEn;
        }
    }
}
