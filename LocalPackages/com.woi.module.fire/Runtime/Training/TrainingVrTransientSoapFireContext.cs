using FireExtinguisher.Core;

namespace Woi.Training
{
    /// <summary>
    /// SOAP no-param event dinleyicileri yangın referansı alamadığı için,
    /// ilgili köprüler <see cref="Raise"/> çağrılmadan hemen önce buraya yazar (VR world kart konumu).
    /// </summary>
    public static class TrainingVrTransientSoapFireContext
    {
        public static FireSource LastWrongTubeFire;
        public static FireSource LastFullyExtinguishedFire;
    }
}
