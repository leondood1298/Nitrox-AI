using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class MedicalCabinetMetadataExtractor : EntityMetadataExtractor<MedicalCabinet, MedicalCabinetMetadata>
{
    public override MedicalCabinetMetadata Extract(MedicalCabinet cabinet)
    {
        // Until MedicalCabinet.Start has run (a cabinet placed a moment ago) there is no state yet; each game starts it
        // from its own clock then.
        if (cabinet.timeSpawnMedKit < 0)
        {
            return null;
        }

        // Taking the medkit closes the door two seconds later (MedicalCabinet.OnHandClick). That door is what the
        // others and later joiners should see, not the open one of the moment.
        bool doorOpen = cabinet.IsInvoking(nameof(MedicalCabinet.ToggleDoorState)) ? !cabinet.doorOpen : cabinet.doorOpen;
        return new MedicalCabinetMetadata(doorOpen, cabinet.hasMedKit, cabinet.timeSpawnMedKit);
    }
}
