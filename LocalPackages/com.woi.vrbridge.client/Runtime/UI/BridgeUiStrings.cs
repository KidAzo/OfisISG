using System.Collections.Generic;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.UI
{
    internal static class BridgeUiStrings
    {
        static readonly Dictionary<string, (string En, string Tr)> Catalog = new Dictionary<string, (string, string)>
        {
            ["pairing.brand"] = ("WOI VR Bridge", "WOI VR Bridge"),
            ["pairing.title"] = ("Pairing Required", "Eşleştirme Gerekli"),
            ["pairing.instructions"] = ("Enter the 6-digit code generated on the Operator Console.", "Operatör ekranında oluşturulan 6 haneli kodu girin."),
            ["pairing.submit"] = ("Pair", "Eşleştir"),
            ["pairing.retry"] = ("Retry", "Yeniden Dene"),
            ["pairing.repair"] = ("Re-pair Device", "Cihazı Yeniden Eşleştir"),
            ["pairing.cancel"] = ("Cancel", "İptal"),
            ["pairing.waiting"] = ("Waiting for pairing code…", "Eşleştirme kodu bekleniyor…"),
            ["diagnostics.title"] = ("Bridge Diagnostics", "Bridge Tanılama"),
            ["diagnostics.state"] = ("State", "Durum"),
            ["diagnostics.deviceId"] = ("Device ID", "Cihaz Kimliği"),
            ["diagnostics.deviceName"] = ("Device Name", "Cihaz Adı"),
            ["diagnostics.gateway"] = ("Bridge IP / Gateway", "Bridge IP / Ağ Geçidi"),
            ["diagnostics.serviceId"] = ("Service ID", "Servis Kimliği"),
            ["diagnostics.rtt"] = ("RTT", "RTT"),
            ["diagnostics.heartbeat"] = ("Last Heartbeat", "Son Kalp Atışı"),
            ["diagnostics.reconnect"] = ("Reconnect Count", "Yeniden Bağlanma"),
            ["diagnostics.pendingResults"] = ("Pending Results", "Bekleyen Sonuçlar"),
            ["diagnostics.session"] = ("Active Session", "Aktif Oturum"),
            ["diagnostics.error"] = ("Error Code", "Hata Kodu"),
            ["diagnostics.action"] = ("Suggested Action", "Önerilen Aksiyon"),
            ["diagnostics.retry"] = ("Retry Connection", "Bağlantıyı Yeniden Dene"),
            ["diagnostics.repair"] = ("Re-pair Device", "Cihazı Yeniden Eşleştir"),
            ["diagnostics.copyError"] = ("Copy / Show Error Code", "Hata Kodunu Kopyala"),
            ["diagnostics.resetIdentity"] = ("Reset Device Identity", "Cihaz Kimliğini Sıfırla"),
            ["diagnostics.resetConfirm"] = ("Reset device identity? Pairing will be required again.", "Cihaz kimliği sıfırlansın mı? Yeniden eşleştirme gerekir."),
            ["diagnostics.resetConfirm2"] = ("Tap again to confirm reset.", "Sıfırlamayı onaylamak için tekrar dokunun."),
            ["diagnostics.export"] = ("Export Diagnostics", "Tanılamayı Dışa Aktar"),
            ["common.close"] = ("Close", "Kapat"),
            ["state.PairingRequired"] = ("Pairing Required", "Eşleştirme Gerekli"),
            ["state.Connected"] = ("Connected", "Bağlı"),
            ["state.Reconnecting"] = ("Reconnecting", "Yeniden bağlanılıyor"),
            ["state.Offline"] = ("Offline", "Çevrimdışı"),
            ["state.Degraded"] = ("Degraded", "Bozulmuş bağlantı"),

            // Phase 2D — authorized training panel (session.authorize.command → player start).
            ["authorized.brand"] = ("WOI VR Bridge", "WOI VR Bridge"),
            ["authorized.participantLabel"] = ("Participant", "Katılımcı"),
            ["authorized.personnelLabel"] = ("Personnel ID", "Personel Kimliği"),
            ["authorized.start"] = ("Start Training", "Eğitimi Başlat"),
            ["authorized.status.ready"] = ("Training ready", "Eğitim hazır"),
            ["authorized.status.starting"] = ("Starting training…", "Eğitim başlatılıyor…"),
            ["authorized.status.started"] = ("Training in progress", "Eğitim devam ediyor"),
            ["authorized.status.revoked"] = ("Authorization revoked. Ask the Operator Console to re-authorize.", "Yetkilendirme iptal edildi. Operatör Konsolu'ndan yeniden yetkilendirme isteyin."),
            ["authorized.status.expired"] = ("Authorization expired. Ask the Operator Console to re-authorize.", "Yetkilendirmenin süresi doldu. Operatör Konsolu'ndan yeniden yetkilendirme isteyin."),
            ["authorized.status.failed"] = ("Could not start training. Ask the Operator Console to re-authorize.", "Eğitim başlatılamadı. Operatör Konsolu'ndan yeniden yetkilendirme isteyin."),
            ["authorized.module.fire-training"] = ("Fire Training", "Yangın Eğitimi"),
            ["authorized.module.default"] = ("Training Module", "Eğitim Modülü")
        };

        public static string Get(string key, string locale, IBridgeStringLocalizer localizer = null)
        {
            if (localizer != null)
            {
                var localized = localizer.Get(key, null);
                if (!string.IsNullOrEmpty(localized))
                {
                    return localized;
                }
            }

            if (!Catalog.TryGetValue(key, out var pair))
            {
                return key;
            }

            return string.Equals(locale, "tr", System.StringComparison.OrdinalIgnoreCase) ? pair.Tr : pair.En;
        }

        /// <summary>Localized display name for a moduleId, falling back to the raw id when unknown.</summary>
        public static string GetModuleName(string moduleId, string locale, IBridgeStringLocalizer localizer = null)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return Get("authorized.module.default", locale, localizer);
            }

            var key = "authorized.module." + moduleId.ToLowerInvariant();
            if (Catalog.ContainsKey(key))
            {
                return Get(key, locale, localizer);
            }

            return moduleId;
        }
    }
}
