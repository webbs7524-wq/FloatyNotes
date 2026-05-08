using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zenject;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace FloatyNotes.Menu;

internal class FloatingNotesController : IInitializable, IDisposable
{
    private const int NoteCount = 60;

    private GameObject? root;

    public void Initialize()
    {
        Plugin.Log.Info($"{nameof(FloatingNotesController)} initialized");

        root = new GameObject(nameof(FloatyNotes));

        var field = root.AddComponent<FloatingNotesField>();
        field.Spawn(NoteCount);
    }

    public void Dispose()
    {
        if (root != null)
        {
            Object.Destroy(root);
            root = null;
        }

        Plugin.Log.Debug($"{nameof(FloatingNotesController)} disposed");
    }
}

internal class FloatingNotesField : MonoBehaviour
{
    private const float DefaultNoteScale = 1f;
    private const float FloorRestingNoteCenterY = 0.5f;
    private const int NotesPerPile = 8;

    private const string NormalGameNoteAddress =
        "Packages/com.beatgames.beatsaber.main.core/Prefabs/SongElements/Notes/NormalGameNote.prefab";

    private static readonly Vector3[] PileCenters =
    {
        new(-6.35f, FloorRestingNoteCenterY, 1.85f),
        new(-3.15f, FloorRestingNoteCenterY, 3.25f),
        new(0.0f, FloorRestingNoteCenterY, 2.65f),
        new(3.25f, FloorRestingNoteCenterY, 3.1f),
        new(6.15f, FloorRestingNoteCenterY, 4.55f)
    };

    private static readonly float[] PileYawAngles =
    {
        -12f,
        8f,
        -4f,
        13f,
        -16f
    };

    private static readonly Vector3[] PileLocalOffsets =
    {
        new(-1.18f, 0f, -0.18f),
        new(-0.42f, 0f, 0.1f),
        new(0.38f, 0f, -0.08f),
        new(1.12f, 0f, 0.16f),
        new(-0.82f, 0f, 0.84f),
        new(0.04f, 0f, 0.9f),
        new(0.76f, 0.64f, 0.54f),
        new(-0.12f, 0.68f, 0.36f)
    };

    private static readonly Vector3[] PileBaseRotations =
    {
        new(-1f, -18f, 2f),
        new(0f, 8f, -1f),
        new(1f, 29f, 1f),
        new(-2f, 52f, -2f),
        new(2f, -35f, -1f),
        new(-1f, 18f, 2f),
        new(-9f, 71f, 14f),
        new(11f, -56f, -13f)
    };

    private static readonly Color[] LightPalette =
    {
        new(1.00f, 0.73f, 0.78f),
        new(1.00f, 0.86f, 0.62f),
        new(1.00f, 0.97f, 0.67f),
        new(0.72f, 0.95f, 0.79f),
        new(0.68f, 0.90f, 1.00f),
        new(0.78f, 0.79f, 1.00f),
        new(0.92f, 0.76f, 1.00f),
        new(0.95f, 0.89f, 0.77f)
    };

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    private readonly List<FloatingNote> notes = new();

    private Material? bodyMaterialTemplate;
    private Material? iconMaterialTemplate;
    private AsyncOperationHandle<GameObject> notePrefabHandle;
    private bool hasNotePrefabHandle;

    public void Spawn(int count)
    {
        bodyMaterialTemplate = CreateMaterial(Color.white);
        iconMaterialTemplate = CreateMaterial(Color.white);

        StartCoroutine(SpawnWhenPrefabIsReady(count));
    }

    private IEnumerator SpawnWhenPrefabIsReady(int count)
    {
        notePrefabHandle = Addressables.LoadAssetAsync<GameObject>(NormalGameNoteAddress);
        hasNotePrefabHandle = true;

        yield return notePrefabHandle;

        var notePrefab = notePrefabHandle.Status == AsyncOperationStatus.Succeeded ? notePrefabHandle.Result : null;
        if (notePrefab == null)
        {
            Plugin.Log.Warn("Could not load Beat Saber's NormalGameNote prefab. Falling back to generated note visuals.");
        }

        for (var i = 0; i < count; i++)
        {
            notes.Add(CreateNote(i, notePrefab));
        }
    }

    private void Update()
    {
        var time = Time.time;

        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var drift = new Vector3(
                Mathf.Sin(time * note.HorizontalSpeed + note.Phase) * note.HorizontalDrift,
                Mathf.Sin(time * note.VerticalSpeed + note.Phase * 0.7f) * note.VerticalDrift,
                Mathf.Cos(time * note.DepthSpeed + note.Phase) * note.DepthDrift);

            note.Transform.localPosition = note.Anchor + drift;
            note.Transform.localRotation =
                Quaternion.Euler(note.BaseRotation + new Vector3(0f, 0f, Mathf.Sin(time * note.TiltSpeed + note.Phase) * note.TiltDrift));
        }
    }

    private void OnDestroy()
    {
        if (hasNotePrefabHandle)
        {
            Addressables.Release(notePrefabHandle);
            hasNotePrefabHandle = false;
        }

        if (bodyMaterialTemplate != null)
        {
            Destroy(bodyMaterialTemplate);
            bodyMaterialTemplate = null;
        }

        if (iconMaterialTemplate != null)
        {
            Destroy(iconMaterialTemplate);
            iconMaterialTemplate = null;
        }
    }

    private FloatingNote CreateNote(int index, GameObject? notePrefab)
    {
        var noteObject = new GameObject($"FloatingGameNote_{index + 1:00}");
        noteObject.transform.SetParent(transform, false);

        var placement = CreatePlacement(index);
        var note = new FloatingNote(
            noteObject.transform,
            placement.Anchor,
            placement.BaseRotation,
            Random.Range(0f, Mathf.PI * 2f),
            placement.IsPileNote ? 0f : Random.Range(0.10f, 0.36f),
            placement.IsPileNote ? 0f : Random.Range(0.10f, 0.28f),
            placement.IsPileNote ? 0f : Random.Range(0.06f, 0.20f),
            placement.IsPileNote ? 0f : Random.Range(0.24f, 0.58f),
            placement.IsPileNote ? 0f : Random.Range(0.38f, 0.78f),
            placement.IsPileNote ? 0f : Random.Range(0.18f, 0.42f),
            placement.IsPileNote ? 0f : Random.Range(0.22f, 0.55f),
            placement.IsPileNote ? 0f : 8f);

        noteObject.transform.localPosition = note.Anchor;
        noteObject.transform.localRotation = Quaternion.Euler(note.BaseRotation);

        var size = DefaultNoteScale;
        var color = LightPalette[Random.Range(0, LightPalette.Length)];
        var cutDirection = RandomCutDirection();

        if (notePrefab != null)
        {
            CreateGameNoteFromPrefab(noteObject.transform, notePrefab, color, size, cutDirection);
        }
        else
        {
            CreateGeneratedGameNote(noteObject.transform, color, size, cutDirection);
        }

        return note;
    }

    private NotePlacement CreatePlacement(int index)
    {
        var pileNoteCount = PileCenters.Length * NotesPerPile;
        if (index < pileNoteCount)
        {
            var pileIndex = index / NotesPerPile;
            var pileNoteIndex = index % NotesPerPile;
            var center = PileCenters[pileIndex];
            var pileYaw = PileYawAngles[pileIndex];
            var offset = RotateAroundY(PileLocalOffsets[pileNoteIndex], pileYaw);
            var jitter = new Vector3(Random.Range(-0.06f, 0.06f), 0f, Random.Range(-0.06f, 0.06f));
            var anchor = center + offset + RotateAroundY(jitter, pileYaw);
            var baseRotation = PileBaseRotations[pileNoteIndex];
            var rotation = new Vector3(
                baseRotation.x + Random.Range(-2f, 2f),
                baseRotation.y + pileYaw + Random.Range(-5f, 5f),
                baseRotation.z + Random.Range(-2f, 2f));

            return new NotePlacement(anchor, rotation, true);
        }

        var floatingIndex = index - pileNoteCount;
        var floatingAnchor = RandomFloatingMenuPosition(floatingIndex);
        return new NotePlacement(floatingAnchor, RandomFloatingMenuRotation(floatingAnchor), false);
    }

    private static Vector3 RandomFloatingMenuPosition(int index)
    {
        var zone = index % 12;
        var side = index % 2 == 0 ? -1f : 1f;

        return zone switch
        {
            0 => new Vector3(side * Random.Range(6.2f, 8.6f), Random.Range(0.7f, 3.0f), Random.Range(1.8f, 4.4f)),
            1 => new Vector3(side * Random.Range(4.2f, 7.1f), Random.Range(3.0f, 4.75f), Random.Range(2.5f, 6.8f)),
            2 => new Vector3(Random.Range(-2.4f, 2.4f), Random.Range(3.25f, 5.0f), Random.Range(2.0f, 4.6f)),
            3 => new Vector3(Random.Range(-6.8f, 6.8f), Random.Range(2.55f, 4.55f), Random.Range(5.8f, 9.0f)),
            4 => new Vector3(side * Random.Range(2.2f, 4.9f), Random.Range(0.45f, 2.25f), Random.Range(6.0f, 9.4f)),
            5 => new Vector3(side * Random.Range(7.2f, 9.6f), Random.Range(-0.1f, 1.75f), Random.Range(4.8f, 8.6f)),
            6 => new Vector3(Random.Range(-7.6f, 7.6f), Random.Range(1.3f, 2.8f), Random.Range(8.2f, 11.2f)),
            7 => new Vector3(side * Random.Range(1.4f, 3.4f), Random.Range(2.1f, 3.85f), Random.Range(1.5f, 3.5f)),
            8 => new Vector3(side * Random.Range(5.0f, 7.6f), Random.Range(2.4f, 4.25f), Random.Range(7.2f, 10.5f)),
            9 => new Vector3(Random.Range(-4.4f, 4.4f), Random.Range(0.7f, 2.1f), Random.Range(4.6f, 7.2f)),
            10 => new Vector3(side * Random.Range(3.4f, 6.4f), Random.Range(-0.25f, 1.1f), Random.Range(6.8f, 10.0f)),
            _ => new Vector3(Random.Range(-8.0f, 8.0f), Random.Range(3.8f, 5.3f), Random.Range(6.4f, 10.8f))
        };
    }

    private static Vector3 RandomFloatingMenuRotation(Vector3 anchor)
    {
        var yawTowardCenter = Mathf.Clamp(-anchor.x * 6.5f, -38f, 38f);

        return new Vector3(
            Random.Range(-12f, 12f),
            yawTowardCenter + Random.Range(-8f, 8f),
            Random.Range(-20f, 20f));
    }

    private static Vector3 RotateAroundY(Vector3 value, float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        var sin = Mathf.Sin(radians);
        var cos = Mathf.Cos(radians);

        return new Vector3(
            value.x * cos - value.z * sin,
            value.y,
            value.x * sin + value.z * cos);
    }

    private void CreateGameNoteFromPrefab(
        Transform parent,
        GameObject notePrefab,
        Color color,
        float size,
        FloatingCutDirection cutDirection)
    {
        var visual = Instantiate(notePrefab, parent, false);
        visual.name = "BeatSaberGameNoteVisual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, DirectionToZRotation(cutDirection));
        visual.transform.localScale = Vector3.one * size;

        DisableGameplayComponents(visual);
        ConfigurePrefabCutDirection(visual, cutDirection);
        ApplyColorToRenderers(visual, color);
    }

    private void CreateGeneratedGameNote(
        Transform parent,
        Color color,
        float size,
        FloatingCutDirection cutDirection)
    {
        var noteRoot = new GameObject("GeneratedGameNoteVisual");
        noteRoot.transform.SetParent(parent, false);

        CreateBody(noteRoot.transform, size, color);
        CreateEdgeHighlights(noteRoot.transform, size, Color.Lerp(color, Color.white, 0.52f));
        CreateCutDirectionIcon(noteRoot.transform, size, cutDirection);
    }

    private void CreateBody(Transform parent, float size, Color color)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(parent, false);
        body.transform.localScale = new Vector3(size, size, size * 0.72f);
        RemoveCollider(body);

        var renderer = body.GetComponent<MeshRenderer>();
        renderer.material = new Material(bodyMaterialTemplate!) { color = color };
    }

    private void CreateEdgeHighlights(Transform parent, float size, Color color)
    {
        var stripThickness = size * 0.035f;
        var stripDepth = size * 0.745f;
        var offset = size * 0.52f;

        CreateStrip(parent, "TopHighlight", new Vector3(0f, offset, 0f), new Vector3(size * 0.88f, stripThickness, stripDepth), color);
        CreateStrip(parent, "BottomHighlight", new Vector3(0f, -offset, 0f), new Vector3(size * 0.88f, stripThickness, stripDepth), color);
        CreateStrip(parent, "LeftHighlight", new Vector3(-offset, 0f, 0f), new Vector3(stripThickness, size * 0.88f, stripDepth), color);
        CreateStrip(parent, "RightHighlight", new Vector3(offset, 0f, 0f), new Vector3(stripThickness, size * 0.88f, stripDepth), color);
    }

    private void CreateStrip(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = name;
        strip.transform.SetParent(parent, false);
        strip.transform.localPosition = position;
        strip.transform.localScale = scale;
        RemoveCollider(strip);

        var renderer = strip.GetComponent<MeshRenderer>();
        renderer.material = new Material(iconMaterialTemplate!) { color = color };
    }

    private void CreateCutDirectionIcon(Transform parent, float size, FloatingCutDirection cutDirection)
    {
        var icon = new GameObject(cutDirection == FloatingCutDirection.Any ? "CircleIcon" : "ArrowIcon");
        icon.transform.SetParent(parent, false);
        icon.transform.localPosition = new Vector3(0f, 0f, -size * 0.37f);
        icon.transform.localRotation = Quaternion.Euler(0f, 0f, DirectionToZRotation(cutDirection));

        var meshFilter = icon.AddComponent<MeshFilter>();
        meshFilter.mesh = cutDirection == FloatingCutDirection.Any ? CreateCircleMesh(size * 0.16f) : CreateArrowMesh(size);

        var meshRenderer = icon.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(iconMaterialTemplate!)
        {
            color = Color.white
        };
    }

    private static Mesh CreateArrowMesh(float size)
    {
        var shaftHalfWidth = size * 0.055f;
        var headHalfWidth = size * 0.18f;
        var top = size * 0.23f;
        var neck = size * 0.02f;
        var bottom = -size * 0.23f;

        var mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(0f, top, 0f),
                new Vector3(headHalfWidth, neck, 0f),
                new Vector3(shaftHalfWidth, neck, 0f),
                new Vector3(shaftHalfWidth, bottom, 0f),
                new Vector3(-shaftHalfWidth, bottom, 0f),
                new Vector3(-shaftHalfWidth, neck, 0f),
                new Vector3(-headHalfWidth, neck, 0f)
            },
            triangles = new[]
            {
                0, 1, 6,
                2, 3, 4,
                2, 4, 5
            }
        };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    private static Mesh CreateCircleMesh(float radius)
    {
        const int SegmentCount = 28;
        var vertices = new Vector3[SegmentCount + 1];
        var triangles = new int[SegmentCount * 3];

        vertices[0] = Vector3.zero;
        for (var i = 0; i < SegmentCount; i++)
        {
            var angle = Mathf.PI * 2f * i / SegmentCount;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        for (var i = 0; i < SegmentCount; i++)
        {
            var triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i == SegmentCount - 1 ? 1 : i + 2;
            triangles[triangleIndex + 2] = i + 1;
        }

        var mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    private static void DisableGameplayComponents(GameObject visual)
    {
        var behaviours = visual.GetComponentsInChildren<MonoBehaviour>(true);
        for (var i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = false;
        }

        var components = visual.GetComponentsInChildren<Component>(true);
        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component == null)
            {
                continue;
            }

            var typeName = component.GetType().Name;
            if (typeName.Contains("Collider") || typeName.Contains("Rigidbody") || typeName.Contains("Cuttable"))
            {
                Destroy(component);
            }
        }
    }

    private static void ConfigurePrefabCutDirection(GameObject visual, FloatingCutDirection cutDirection)
    {
        var showCircle = cutDirection == FloatingCutDirection.Any;
        var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            var rendererName = renderer.name.ToLowerInvariant();
            var objectName = renderer.gameObject.name.ToLowerInvariant();
            var combinedName = rendererName + " " + objectName;

            if (combinedName.Contains("arrow"))
            {
                renderer.enabled = !showCircle;
            }
            else if (combinedName.Contains("circle") || combinedName.Contains("dot"))
            {
                renderer.enabled = showCircle;
            }
        }
    }

    private static void ApplyColorToRenderers(GameObject visual, Color color)
    {
        var iconColor = Color.Lerp(color, Color.white, 0.62f);
        var propertyBlock = new MaterialPropertyBlock();
        var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            var targetColor = IsIconRenderer(renderer) ? iconColor : color;

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorId, targetColor);
            propertyBlock.SetColor(BaseColorId, targetColor);
            propertyBlock.SetColor(TintColorId, targetColor);
            renderer.SetPropertyBlock(propertyBlock);

            var materials = renderer.materials;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                ApplyColorToMaterial(materials[materialIndex], targetColor);
            }
        }
    }

    private static bool IsIconRenderer(Renderer renderer)
    {
        var rendererName = renderer.name.ToLowerInvariant();
        var objectName = renderer.gameObject.name.ToLowerInvariant();
        var combinedName = rendererName + " " + objectName;

        return combinedName.Contains("arrow") || combinedName.Contains("circle") || combinedName.Contains("dot");
    }

    private static void ApplyColorToMaterial(Material material, Color color)
    {
        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(TintColorId))
        {
            material.SetColor(TintColorId, color);
        }
    }

    private static void RemoveCollider(GameObject gameObject)
    {
        var collider = gameObject.GetComponent("Collider") ?? gameObject.GetComponent("BoxCollider");
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private static Material CreateMaterial(Color color)
    {
        var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
        var material = new Material(shader)
        {
            color = color
        };

        return material;
    }

    private static FloatingCutDirection RandomCutDirection()
    {
        var values = (FloatingCutDirection[])Enum.GetValues(typeof(FloatingCutDirection));
        return values[Random.Range(0, values.Length)];
    }

    private static float DirectionToZRotation(FloatingCutDirection cutDirection) =>
        cutDirection switch
        {
            FloatingCutDirection.Up => 0f,
            FloatingCutDirection.Down => 180f,
            FloatingCutDirection.Left => 90f,
            FloatingCutDirection.Right => -90f,
            FloatingCutDirection.UpLeft => 45f,
            FloatingCutDirection.UpRight => -45f,
            FloatingCutDirection.DownLeft => 135f,
            FloatingCutDirection.DownRight => -135f,
            _ => 0f
        };

    private enum FloatingCutDirection
    {
        Up,
        Down,
        Left,
        Right,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight,
        Any
    }

    private readonly struct FloatingNote
    {
        public readonly Transform Transform;
        public readonly Vector3 Anchor;
        public readonly Vector3 BaseRotation;
        public readonly float Phase;
        public readonly float HorizontalDrift;
        public readonly float VerticalDrift;
        public readonly float DepthDrift;
        public readonly float HorizontalSpeed;
        public readonly float VerticalSpeed;
        public readonly float DepthSpeed;
        public readonly float TiltSpeed;
        public readonly float TiltDrift;

        public FloatingNote(
            Transform transform,
            Vector3 anchor,
            Vector3 baseRotation,
            float phase,
            float horizontalDrift,
            float verticalDrift,
            float depthDrift,
            float horizontalSpeed,
            float verticalSpeed,
            float depthSpeed,
            float tiltSpeed,
            float tiltDrift)
        {
            Transform = transform;
            Anchor = anchor;
            BaseRotation = baseRotation;
            Phase = phase;
            HorizontalDrift = horizontalDrift;
            VerticalDrift = verticalDrift;
            DepthDrift = depthDrift;
            HorizontalSpeed = horizontalSpeed;
            VerticalSpeed = verticalSpeed;
            DepthSpeed = depthSpeed;
            TiltSpeed = tiltSpeed;
            TiltDrift = tiltDrift;
        }
    }

    private readonly struct NotePlacement
    {
        public readonly Vector3 Anchor;
        public readonly Vector3 BaseRotation;
        public readonly bool IsPileNote;

        public NotePlacement(Vector3 anchor, Vector3 baseRotation, bool isPileNote)
        {
            Anchor = anchor;
            BaseRotation = baseRotation;
            IsPileNote = isPileNote;
        }
    }
}
