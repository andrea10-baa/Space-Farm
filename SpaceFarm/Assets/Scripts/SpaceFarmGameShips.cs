using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>Visual-only landings. State changes happen before an arrival is queued.</summary>
    public sealed partial class SpaceFarmGame
    {
        private const float InitialLandingSeconds = 3.9f;
        private const float ShipmentDescentSeconds = 1.15f;
        private const float ShipmentAscentSeconds = 1.2f;

        private readonly Queue<Vector2Int?> shipmentLandings = new Queue<Vector2Int?>();
        private readonly HashSet<Vector2Int> reservedShipmentCells = new HashSet<Vector2Int>();
        private bool arrivalSequenceActive;
        private bool initialLandingStarted;
        private bool startingBaseLanded;
        private bool shipmentSequenceActive;
        private Sprite landerSprite;
        private Texture2D softParticleTexture;
        private Material softParticleMaterial;
        private CanvasGroup hudInputGroup;
        private Coroutine shipmentQueueRoutine;

        public bool ArrivalSequenceActive => arrivalSequenceActive;
        public bool ShipmentSequenceActive => shipmentSequenceActive || shipmentLandings.Count > 0;

        private void PrepareForInitialLanding()
        {
            SetStartingBaseVisible(false);
            initialLandingStarted = false;
            arrivalSequenceActive = false;
            GetHudInputGroup();
            SetHudControlsEnabled(true);
        }

        private void BeginInitialLanding()
        {
            if (initialLandingStarted || worldRoot == null || gameCamera == null) return;
            initialLandingStarted = true;
            arrivalSequenceActive = true;
            SetHudControlsEnabled(false);
            StartCoroutine(InitialLanding());
        }

        private void SetStartingBaseVisible(bool visible)
        {
            if (habitatWorld != null) habitatWorld.SetActive(visible);
            if (oxygenPlantWorld != null) oxygenPlantWorld.SetActive(visible);
            if (solarWorld != null) solarWorld.SetActive(visible);
            if (dormWorld != null) dormWorld.SetActive(visible);
            if (rover != null) rover.SetActive(visible);
            for (int i = 0; i < warehouseWorlds.Count; i++)
                if (warehouseWorlds[i] != null) warehouseWorlds[i].SetActive(visible);
            for (int i = 0; i < astronauts.Count; i++)
                if (astronauts[i].Root != null) astronauts[i].Root.gameObject.SetActive(visible);
        }

        private CanvasGroup GetHudInputGroup()
        {
            if (hudCanvas == null) return null;
            if (hudInputGroup == null)
            {
                hudInputGroup = hudCanvas.GetComponent<CanvasGroup>();
                if (hudInputGroup == null) hudInputGroup = hudCanvas.gameObject.AddComponent<CanvasGroup>();
            }
            return hudInputGroup;
        }

        private void SetHudControlsEnabled(bool enabled)
        {
            CanvasGroup group = GetHudInputGroup();
            if (group == null) return;
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }

        private IEnumerator InitialLanding()
        {
            Vector3 landing = CellPosition(BaseCell + new Vector2Int(0, -2)) + new Vector3(0f, 0f, -.7f);
            Vector3 start = landing;
            start.y = gameCamera.transform.position.y + gameCamera.orthographicSize + 1.55f;
            GameObject ship = CreateShipVisual("Nave de aterrizaje inicial", start, 2.35f, 1.56f, 17,
                out SpriteRenderer shipRenderer);
            ParticleSystem exhaust = CreateExhaust(ship.transform, 14);
            exhaust.Play();

            yield return MoveShip(ship.transform, start, landing, InitialLandingSeconds, 0f, -3.5f);
            if (exhaust != null) exhaust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            CreateLandingDust(landing);
            float impact = 0f;
            Vector3 settled = landing;
            while (impact < .28f && ship != null)
            {
                impact += Time.deltaTime;
                float bounce = Mathf.Sin(Mathf.Clamp01(impact / .28f) * Mathf.PI) * .08f;
                ship.transform.position = settled + Vector3.up * bounce;
                yield return null;
            }

            yield return FadeShip(shipRenderer, .3f);
            if (ship != null) Destroy(ship);
            SetStartingBaseVisible(true);
            startingBaseLanded = true;
            RefreshWorld();
            RefreshHud();
            arrivalSequenceActive = false;
            SetHudControlsEnabled(true);
            SetReport($"Aterrizaje completado en Marte (semilla {seed}). Enviá el rover a explorar los sectores ocultos.");
        }

        private void QueueShipmentArrival()
        {
            Vector2Int? landing = FindShipmentLandingCell();
            if (landing.HasValue) reservedShipmentCells.Add(landing.Value);
            shipmentLandings.Enqueue(landing);
            if (shipmentQueueRoutine == null) shipmentQueueRoutine = StartCoroutine(PlayShipmentQueue());
        }

        private IEnumerator PlayShipmentQueue()
        {
            shipmentSequenceActive = true;
            while (shipmentLandings.Count > 0)
            {
                Vector2Int? cell = shipmentLandings.Dequeue();
                yield return AnimateShipment(cell);
                if (cell.HasValue) reservedShipmentCells.Remove(cell.Value);
            }
            shipmentSequenceActive = false;
            shipmentQueueRoutine = null;
        }

        private IEnumerator AnimateShipment(Vector2Int? landingCell)
        {
            if (gameCamera == null || worldRoot == null) yield break;

            if (!landingCell.HasValue)
            {
                // With no safe ground available, show a fly-by above the base instead of blocking delivery.
                Vector3 flyByStart = new Vector3(gameCamera.transform.position.x - gameCamera.orthographicSize * gameCamera.aspect - 2f,
                    gameCamera.transform.position.y + gameCamera.orthographicSize * .45f, -.7f);
                Vector3 flyByEnd = flyByStart + Vector3.right * (gameCamera.orthographicSize * gameCamera.aspect * 2f + 4f);
                GameObject flyByShip = CreateShipVisual("Nave de suministros en tránsito", flyByStart, 1.8f, 1.2f, 17,
                    out _);
                ParticleSystem flyByExhaust = CreateExhaust(flyByShip.transform, 9);
                flyByExhaust.Play();
                yield return MoveShip(flyByShip.transform, flyByStart, flyByEnd, 2.8f, 0f, 0f);
                if (flyByShip != null) Destroy(flyByShip);
                yield break;
            }

            Vector3 landing = CellPosition(landingCell.Value) + new Vector3(0f, 0f, -.7f);
            Vector3 start = landing;
            start.y = gameCamera.transform.position.y + gameCamera.orthographicSize + 1.3f;
            GameObject ship = CreateShipVisual("Nave de suministros", start, 1.8f, 1.2f, 17,
                out SpriteRenderer shipRenderer);
            ParticleSystem exhaust = CreateExhaust(ship.transform, 10);
            exhaust.Play();
            yield return MoveShip(ship.transform, start, landing, ShipmentDescentSeconds, 0f, -2.5f);
            if (exhaust != null) exhaust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            CreateLandingDust(landing);

            // The shipment has already been committed in SpaceFarmState.AdvanceSol exactly once.
            yield return new WaitForSeconds(.65f);
            if (ship != null)
            {
                exhaust.Play();
                Vector3 takeoff = landing;
                takeoff.y = gameCamera.transform.position.y + gameCamera.orthographicSize + 1.3f;
                yield return MoveShip(ship.transform, landing, takeoff, ShipmentAscentSeconds, -2.5f, 1.5f);
                Destroy(ship);
            }
        }

        private Vector2Int? FindShipmentLandingCell()
        {
            const int maxRadius = 9;
            for (int radius = 2; radius <= maxRadius; radius++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius) continue;
                Vector2Int candidate = BaseCell + new Vector2Int(x, y);
                if (!map.Inside(candidate) || reservedShipmentCells.Contains(candidate)) continue;
                bool occupied = false;
                for (int offsetY = -1; offsetY <= 1 && !occupied; offsetY++)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                    if (state.IsCellBlocked(candidate + new Vector2Int(offsetX, offsetY)))
                    {
                        occupied = true;
                        break;
                    }
                for (int i = 0; i < map.Deposits.Count && !occupied; i++)
                    if (map.Deposits[i].Cell == candidate) occupied = true;
                if (occupied) continue;
                if (candidate == roverCell || Vector2.Distance(candidate, roverCell) < 2f) continue;
                if (Vector2.Distance(candidate, BaseCell) < 3.5f) continue;

                bool nearCrew = false;
                for (int i = 0; i < astronauts.Count; i++)
                    if (astronauts[i].Root != null && Vector2.Distance(CellPosition(candidate), astronauts[i].Root.position) < 1.6f)
                    {
                        nearCrew = true;
                        break;
                    }
                if (nearCrew) continue;
                return candidate;
            }
            return null;
        }

        private GameObject CreateShipVisual(string name, Vector3 position, float width, float height, int sortingOrder,
            out SpriteRenderer renderer)
        {
            if (landerSprite == null)
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>("Art/lander");
                for (int i = 0; i < sprites.Length; i++)
                    if (sprites[i] != null && sprites[i].name == "lander_0")
                    {
                        landerSprite = sprites[i];
                        break;
                    }
                if (landerSprite == null && sprites.Length > 0) landerSprite = sprites[0];
            }

            GameObject root = new GameObject(name);
            root.transform.SetParent(worldRoot.transform, false);
            root.transform.position = position;
            GameObject body = new GameObject("Módulo de aterrizaje");
            body.transform.SetParent(root.transform, false);
            renderer = body.AddComponent<SpriteRenderer>();
            renderer.sprite = landerSprite != null ? landerSprite : squareSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            Vector2 bounds = renderer.sprite.bounds.size;
            float scale = Mathf.Min(width / Mathf.Max(.01f, bounds.x), height / Mathf.Max(.01f, bounds.y));
            body.transform.localScale = Vector3.one * scale;
            return root;
        }

        private ParticleSystem CreateExhaust(Transform parent, int sortingOrder)
        {
            GameObject exhaustObject = new GameObject("Propulsores");
            exhaustObject.transform.SetParent(parent, false);
            exhaustObject.transform.localPosition = new Vector3(0f, -.52f, .02f);
            exhaustObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ParticleSystem exhaust = exhaustObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = exhaust.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.16f, .38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.12f, .28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .68f, .27f, .82f),
                new Color(.35f, .88f, 1f, .7f));
            main.maxParticles = 64;
            ParticleSystem.EmissionModule emission = exhaust.emission;
            emission.rateOverTime = 22f;
            ParticleSystem.ShapeModule shape = exhaust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 13f;
            shape.radius = .1f;
            ParticleSystemRenderer renderer = exhaust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetSoftParticleMaterial();
            renderer.sortingOrder = sortingOrder - 1;
            return exhaust;
        }

        private void CreateLandingDust(Vector3 position)
        {
            GameObject dustObject = new GameObject("Polvo del aterrizaje");
            dustObject.transform.SetParent(worldRoot.transform, false);
            dustObject.transform.position = position + new Vector3(0f, -.45f, .05f);
            ParticleSystem dust = dustObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = dust.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(.18f, .5f);
            main.startColor = new Color(.72f, .39f, .28f, .58f);
            main.maxParticles = 36;
            ParticleSystem.EmissionModule emission = dust.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .28f;
            ParticleSystemRenderer renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetSoftParticleMaterial();
            renderer.sortingOrder = 15;
            dust.Play();
            dust.Emit(24);
            Destroy(dustObject, 1.4f);
        }

        private Material GetSoftParticleMaterial()
        {
            if (softParticleMaterial != null) return softParticleMaterial;
            softParticleTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                name = "Soft radial particle texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                float alpha = 1f - Mathf.SmoothStep(.38f, 1f, distance);
                pixels[y * 64 + x] = new Color(1f, 1f, 1f, alpha);
            }
            softParticleTexture.SetPixels(pixels);
            softParticleTexture.Apply(false, true);

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            softParticleMaterial = new Material(shader) { name = "Soft Mars particle material" };
            if (softParticleMaterial.HasProperty("_MainTex")) softParticleMaterial.SetTexture("_MainTex", softParticleTexture);
            if (softParticleMaterial.HasProperty("_BaseMap")) softParticleMaterial.SetTexture("_BaseMap", softParticleTexture);
            runtimeAssets.Add(softParticleTexture);
            runtimeAssets.Add(softParticleMaterial);
            return softParticleMaterial;
        }

        private static IEnumerator MoveShip(Transform ship, Vector3 from, Vector3 to, float duration,
            float startAngle, float endAngle)
        {
            float elapsed = 0f;
            while (elapsed < duration && ship != null)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                ship.position = Vector3.LerpUnclamped(from, to, eased);
                ship.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startAngle, endAngle, eased));
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (ship != null)
            {
                ship.position = to;
                ship.rotation = Quaternion.Euler(0f, 0f, endAngle);
            }
        }

        private static IEnumerator FadeShip(SpriteRenderer renderer, float duration)
        {
            if (renderer == null) yield break;
            Color color = renderer.color;
            float elapsed = 0f;
            while (elapsed < duration && renderer != null)
            {
                elapsed += Time.deltaTime;
                color.a = 1f - Mathf.Clamp01(elapsed / duration);
                renderer.color = color;
                yield return null;
            }
        }
    }
}
