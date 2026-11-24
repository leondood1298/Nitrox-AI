using System.Collections.Generic;
using UnityEngine;

namespace NitroxClient.GameLogic.PlayerLogic.PlayerModel;

/// <summary>
///     Frees the materials and textures made for one player's body. Unity does not destroy them with the body: the
///     colour swap instances a material on each suit renderer and clones its texture, which stayed in memory after every
///     player who left.
/// </summary>
public static class BodyRenderResources
{
    /// <summary>
    ///     Starts the name of every texture cloned for a player's colour, so that only those are ever destroyed.
    /// </summary>
    public const string CLONED_TEXTURE_PREFIX = "NitroxPlayerColor_";

    /// <summary>
    ///     A material on the body that the prototype it was copied from does not have was made for this body. Materials of
    ///     anything else the body carries (a held tool, an item) are not touched: they may be shared with other objects.
    /// </summary>
    public static void Release(GameObject body, GameObject prototype)
    {
        if (!body || !prototype)
        {
            return;
        }

        HashSet<Object> toDestroy = [];
        foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            Renderer prototypeRenderer = FindCounterpart(renderer.transform, body.transform, prototype.transform);
            if (!prototypeRenderer)
            {
                continue;
            }

            HashSet<Material> prototypeMaterials = [.. prototypeRenderer.sharedMaterials];
            foreach (Material material in renderer.sharedMaterials)
            {
                if (!material || prototypeMaterials.Contains(material))
                {
                    continue;
                }
                toDestroy.Add(material);
                foreach (int textureId in material.GetTexturePropertyNameIDs())
                {
                    Texture texture = material.GetTexture(textureId);
                    if (texture && texture.name.StartsWith(CLONED_TEXTURE_PREFIX))
                    {
                        toDestroy.Add(texture);
                    }
                }
            }
        }

        foreach (Object resource in toDestroy)
        {
            Object.Destroy(resource);
        }
    }

    private static Renderer FindCounterpart(Transform part, Transform body, Transform prototype)
    {
        List<string> names = [];
        Transform current = part;
        while (current && current != body)
        {
            names.Add(current.name);
            current = current.parent;
        }
        names.Reverse();
        Transform counterpart = names.Count == 0 ? prototype : prototype.Find(string.Join("/", names));
        return counterpart ? counterpart.GetComponent(part.GetComponent<Renderer>().GetType()) as Renderer : null;
    }
}
