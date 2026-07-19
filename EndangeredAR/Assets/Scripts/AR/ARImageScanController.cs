using System;
using UnityEngine;
using UnityEngine.UI;

namespace EndangeredAR.AR
{
    public class ARImageScanController : MonoBehaviour
    {
        [SerializeField] private string defaultAnimalId = "sensen";
        [SerializeField] private bool enableCameraRecognition = true;
        [SerializeField] private int requestedCameraWidth = 1280;
        [SerializeField] private int requestedCameraHeight = 720;
        [SerializeField] private float scanIntervalSeconds = 0.22f;
        [SerializeField] private int requiredStableDetections = 3;
        [SerializeField] private MarkerAnimalMapping[] markerAnimals =
        {
            new MarkerAnimalMapping("sensen_marker", "sensen"),
            new MarkerAnimalMapping("animal_02_marker", "animal_02"),
            new MarkerAnimalMapping("animal_03_marker", "animal_03")
        };

        public event Action<string> AnimalMarkerDetected;
        public event Action<string, Transform> AnimalMarkerTracked;

        private WebCamTexture cameraTexture;
        private RawImage cameraPreview;
        private float nextScanTime;
        private int stableDetectionCount;
        private bool markerFired;

        public bool BeginCameraScanning(RawImage preview)
        {
            cameraPreview = preview;
            stableDetectionCount = 0;
            markerFired = false;
            nextScanTime = 0f;

            if (!enableCameraRecognition)
            {
                return false;
            }

            if (cameraTexture == null)
            {
                var devices = WebCamTexture.devices;
                if (devices == null || devices.Length == 0)
                {
                    return false;
                }

                var deviceName = devices[0].name;
                for (var i = 0; i < devices.Length; i++)
                {
                    if (!devices[i].isFrontFacing)
                    {
                        deviceName = devices[i].name;
                        break;
                    }
                }

                cameraTexture = new WebCamTexture(deviceName, requestedCameraWidth, requestedCameraHeight, 30);
            }

            if (cameraPreview != null)
            {
                cameraPreview.texture = cameraTexture;
                cameraPreview.color = Color.white;
            }

            if (!cameraTexture.isPlaying)
            {
                cameraTexture.Play();
            }

            return true;
        }

        public void StopCameraScanning()
        {
            stableDetectionCount = 0;
            markerFired = false;

            if (cameraPreview != null)
            {
                cameraPreview.texture = null;
            }

            if (cameraTexture != null && cameraTexture.isPlaying)
            {
                cameraTexture.Stop();
            }
        }

        public void SimulateMarkerDetected()
        {
            SimulateMarkerDetected(defaultAnimalId);
        }

        public void SimulateMarkerDetected(string animalIdOrMarkerName)
        {
            markerFired = true;
            var resolvedAnimalId = ResolveAnimalId(animalIdOrMarkerName);
            AnimalMarkerDetected?.Invoke(resolvedAnimalId);
            AnimalMarkerTracked?.Invoke(resolvedAnimalId, transform);
        }

        private void Update()
        {
            if (!enableCameraRecognition || markerFired || cameraTexture == null || !cameraTexture.isPlaying)
            {
                return;
            }

            ApplyPreviewOrientation();

            if (Time.unscaledTime < nextScanTime || !cameraTexture.didUpdateThisFrame)
            {
                return;
            }

            nextScanTime = Time.unscaledTime + scanIntervalSeconds;
            if (DetectSensenMarker())
            {
                stableDetectionCount++;
                if (stableDetectionCount >= requiredStableDetections)
                {
                    SimulateMarkerDetected();
                }
            }
            else
            {
                stableDetectionCount = Mathf.Max(0, stableDetectionCount - 1);
            }
        }

        private void ApplyPreviewOrientation()
        {
            if (cameraPreview == null)
            {
                return;
            }

            var rect = cameraPreview.rectTransform;
            rect.localEulerAngles = new Vector3(0f, 0f, -cameraTexture.videoRotationAngle);
            var aspect = cameraPreview.GetComponent<AspectRatioFitter>();
            if (aspect != null && cameraTexture.width > 16 && cameraTexture.height > 16)
            {
                var rotated = Mathf.Abs(cameraTexture.videoRotationAngle) == 90 || Mathf.Abs(cameraTexture.videoRotationAngle) == 270;
                aspect.aspectRatio = rotated
                    ? cameraTexture.height / (float)cameraTexture.width
                    : cameraTexture.width / (float)cameraTexture.height;
            }

            cameraPreview.uvRect = cameraTexture.videoVerticallyMirrored
                ? new Rect(0f, 1f, 1f, -1f)
                : new Rect(0f, 0f, 1f, 1f);
        }

        private bool DetectSensenMarker()
        {
            if (cameraTexture.width < 160 || cameraTexture.height < 120)
            {
                return false;
            }

            var pixels = cameraTexture.GetPixels32();
            var width = cameraTexture.width;
            var height = cameraTexture.height;
            var cropX = Mathf.RoundToInt(width * 0.12f);
            var cropY = Mathf.RoundToInt(height * 0.18f);
            var cropW = Mathf.RoundToInt(width * 0.76f);
            var cropH = Mathf.RoundToInt(height * 0.64f);

            var cornerHits = 0;
            cornerHits += RegionDarkRatio(pixels, width, cropX, cropY, cropW, cropH, 0.04f, 0.06f, 0.22f, 0.24f) > 0.28f ? 1 : 0;
            cornerHits += RegionDarkRatio(pixels, width, cropX, cropY, cropW, cropH, 0.74f, 0.06f, 0.22f, 0.24f) > 0.28f ? 1 : 0;
            cornerHits += RegionDarkRatio(pixels, width, cropX, cropY, cropW, cropH, 0.04f, 0.70f, 0.22f, 0.24f) > 0.28f ? 1 : 0;
            cornerHits += RegionDarkRatio(pixels, width, cropX, cropY, cropW, cropH, 0.74f, 0.70f, 0.22f, 0.24f) > 0.28f ? 1 : 0;

            var centerGreenRatio = RegionGreenRatio(pixels, width, cropX, cropY, cropW, cropH, 0.34f, 0.32f, 0.32f, 0.34f);
            var centerContrast = RegionLumaRange(pixels, width, cropX, cropY, cropW, cropH, 0.28f, 0.20f, 0.44f, 0.48f);

            return cornerHits >= 3 && (centerGreenRatio > 0.045f || centerContrast > 115f);
        }

        private static float RegionDarkRatio(Color32[] pixels, int width, int cropX, int cropY, int cropW, int cropH, float rx, float ry, float rw, float rh)
        {
            return RegionRatio(pixels, width, cropX, cropY, cropW, cropH, rx, ry, rw, rh, pixel => Luma(pixel) < 72f);
        }

        private static float RegionGreenRatio(Color32[] pixels, int width, int cropX, int cropY, int cropW, int cropH, float rx, float ry, float rw, float rh)
        {
            return RegionRatio(pixels, width, cropX, cropY, cropW, cropH, rx, ry, rw, rh, pixel =>
                pixel.g > 72 && pixel.g > pixel.r * 1.18f && pixel.g > pixel.b * 1.18f);
        }

        private static float RegionRatio(Color32[] pixels, int width, int cropX, int cropY, int cropW, int cropH, float rx, float ry, float rw, float rh, Func<Color32, bool> predicate)
        {
            var x0 = cropX + Mathf.RoundToInt(cropW * rx);
            var y0 = cropY + Mathf.RoundToInt(cropH * ry);
            var x1 = x0 + Mathf.Max(2, Mathf.RoundToInt(cropW * rw));
            var y1 = y0 + Mathf.Max(2, Mathf.RoundToInt(cropH * rh));
            var total = 0;
            var matches = 0;

            for (var y = y0; y < y1; y += 4)
            {
                var row = y * width;
                for (var x = x0; x < x1; x += 4)
                {
                    total++;
                    if (predicate(pixels[row + x]))
                    {
                        matches++;
                    }
                }
            }

            return total == 0 ? 0f : (float)matches / total;
        }

        private static float RegionLumaRange(Color32[] pixels, int width, int cropX, int cropY, int cropW, int cropH, float rx, float ry, float rw, float rh)
        {
            var x0 = cropX + Mathf.RoundToInt(cropW * rx);
            var y0 = cropY + Mathf.RoundToInt(cropH * ry);
            var x1 = x0 + Mathf.Max(2, Mathf.RoundToInt(cropW * rw));
            var y1 = y0 + Mathf.Max(2, Mathf.RoundToInt(cropH * rh));
            var min = 255f;
            var max = 0f;

            for (var y = y0; y < y1; y += 4)
            {
                var row = y * width;
                for (var x = x0; x < x1; x += 4)
                {
                    var luma = Luma(pixels[row + x]);
                    min = Mathf.Min(min, luma);
                    max = Mathf.Max(max, luma);
                }
            }

            return max - min;
        }

        private static float Luma(Color32 pixel)
        {
            return pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
        }

        private string ResolveAnimalId(string referenceImageName)
        {
            if (string.IsNullOrWhiteSpace(referenceImageName))
            {
                return defaultAnimalId;
            }

            var normalized = referenceImageName.ToLowerInvariant();
            if (markerAnimals != null)
            {
                foreach (var mapping in markerAnimals)
                {
                    if (mapping != null && mapping.Matches(normalized))
                    {
                        return mapping.AnimalId;
                    }
                }
            }

            if (normalized.Contains("sensen"))
            {
                return "sensen";
            }

            if (normalized.Contains("animal_02") || normalized.Contains("animal02"))
            {
                return "animal_02";
            }

            if (normalized.Contains("animal_03") || normalized.Contains("animal03"))
            {
                return "animal_03";
            }

            return defaultAnimalId;
        }
    }

    [Serializable]
    public class MarkerAnimalMapping
    {
        public MarkerAnimalMapping()
        {
        }

        public MarkerAnimalMapping(string markerName, string animalId)
        {
            this.markerName = markerName;
            this.animalId = animalId;
        }

        [SerializeField] private string markerName;
        [SerializeField] private string animalId;

        public string AnimalId => string.IsNullOrWhiteSpace(animalId) ? "sensen" : animalId;

        public bool Matches(string normalizedReferenceName)
        {
            if (string.IsNullOrWhiteSpace(markerName) || string.IsNullOrWhiteSpace(normalizedReferenceName))
            {
                return false;
            }

            return normalizedReferenceName.Contains(markerName.ToLowerInvariant());
        }
    }
}
