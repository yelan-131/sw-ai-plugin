using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SwComAddin.Models;

namespace SwComAddin.Views
{
    public partial class PartPreviewView : UserControl
    {
        public PartPreviewView()
        {
            InitializeComponent();
        }

        public void ShowPart(string partName, string categoryName,
            Dictionary<string, object> specs, string featureTemplate)
        {
            PreviewTitle.Text = string.Format("数模预览 — {0}", partName);

            if (string.IsNullOrEmpty(featureTemplate) || specs == null)
            {
                NoPreviewText.Visibility = Visibility.Visible;
                PreviewViewport.Visibility = Visibility.Collapsed;
                return;
            }

            var features = PartFeatureTemplates.Build(featureTemplate, specs);
            if (features == null || features.Features.Count == 0)
            {
                NoPreviewText.Visibility = Visibility.Visible;
                PreviewViewport.Visibility = Visibility.Collapsed;
                return;
            }

            NoPreviewText.Visibility = Visibility.Collapsed;
            PreviewViewport.Visibility = Visibility.Visible;

            var renderer = new PreviewRenderer();
            renderer.Render(features, PreviewViewport);
        }

        // Backward compat: called from Part_MouseDown with old 3-arg signature
        public void ShowPart(string partName, string categoryName,
            Dictionary<string, object> specs)
        {
            ShowPart(partName, categoryName, specs, null);
        }
    }

    // === PreviewRenderer: FeatureList → WPF 3D ===

    public class PreviewRenderer
    {
        private double _sceneScale = 1.0;

        public void Render(FeatureList features, Viewport3D viewport)
        {
            viewport.Children.Clear();

            // Model group
            var modelGroup = new Model3DGroup();

            // Lights
            modelGroup.Children.Add(new AmbientLight(Colors.LightGray));
            modelGroup.Children.Add(new DirectionalLight(
                Colors.White, new Vector3D(-1, -1, -1)));
            modelGroup.Children.Add(new DirectionalLight(
                Color.FromRgb(180, 180, 180), new Vector3D(1, 1, 0.5)));

            // Calculate bounding box for auto-scale
            double maxDim = 1;
            double offsetY = 0;

            // Render each feature
            foreach (var feature in features.Features)
            {
                MeshGeometry3D mesh = null;
                Material material = null;

                if (feature is ExtrudeFeature ef)
                {
                    mesh = CreateMeshForProfile(ef.Profile, ef.Depth);
                    material = new DiffuseMaterial(
                        new SolidColorBrush(Color.FromRgb(100, 149, 237)));
                    offsetY += ef.Depth;

                    // Track max dimension for camera
                    double profileSize = GetProfileSize(ef.Profile);
                    if (profileSize > maxDim) maxDim = profileSize;
                    if (ef.Depth > maxDim) maxDim = ef.Depth;
                }
                else if (feature is ExtrudeCutFeature ecf)
                {
                    mesh = CreateMeshForProfile(ecf.Profile, ecf.Depth);
                    material = new DiffuseMaterial(
                        new SolidColorBrush(Color.FromRgb(60, 60, 60)));
                }
                else if (feature is ChamferFeature || feature is FilletFeature)
                {
                    // Chamfer and fillet are visual hints - skip for simplified preview
                    continue;
                }

                if (mesh != null && material != null)
                {
                    var geoModel = new GeometryModel3D(mesh, material);
                    // Add wireframe overlay
                    geoModel.BackMaterial = material;
                    modelGroup.Children.Add(geoModel);
                }
            }

            // Add wireframe
            AddWireframe(modelGroup, features, ref maxDim);

            // Set up model visual
            var visual = new ModelVisual3D { Content = modelGroup };
            viewport.Children.Add(visual);

            // Camera
            _sceneScale = maxDim * 0.8;
            double camDist = maxDim * 2.5;
            viewport.Camera = new PerspectiveCamera(
                new Point3D(camDist, camDist * 0.7, camDist * 0.7),
                new Vector3D(-1, -0.7, -0.7),
                new Vector3D(0, 1, 0),
                45);

            // Mouse rotation
            viewport.MouseLeftButtonDown -= Viewport_MouseDown;
            viewport.MouseMove -= Viewport_MouseMove;
            viewport.MouseLeftButtonUp -= Viewport_MouseUp;
            viewport.MouseLeftButtonDown += Viewport_MouseDown;
            viewport.MouseMove += Viewport_MouseMove;
            viewport.MouseLeftButtonUp += Viewport_MouseUp;
        }

        // === Mesh Generation ===

        private MeshGeometry3D CreateMeshForProfile(Profile profile, double depth)
        {
            if (profile is CircleProfile cp)
                return CreateCylinder(cp.Diameter, depth);
            if (profile is PolygonProfile pp)
                return CreatePrism(pp.Sides, pp.Diameter, depth);
            if (profile is RectangleProfile rp)
                return CreateBox(rp.Width, rp.Height, depth);
            return null;
        }

        private double GetProfileSize(Profile profile)
        {
            if (profile is CircleProfile cp) return cp.Diameter;
            if (profile is PolygonProfile pp) return pp.Diameter;
            if (profile is RectangleProfile rp) return System.Math.Max(rp.Width, rp.Height);
            return 10;
        }

        private MeshGeometry3D CreateCylinder(double diameter, double height, int segments = 24)
        {
            double radius = diameter / 2;
            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            // Center top (index 0) and center bottom (index 1)
            positions.Add(new Point3D(0, height, 0));
            positions.Add(new Point3D(0, 0, 0));
            normals.Add(new Vector3D(0, 1, 0));
            normals.Add(new Vector3D(0, -1, 0));

            // Side vertices (top ring starts at 2, bottom ring starts at 2+segments)
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * System.Math.PI * i / segments;
                double x = radius * System.Math.Cos(angle);
                double z = radius * System.Math.Sin(angle);
                positions.Add(new Point3D(x, height, z));
                normals.Add(new Vector3D(x, 0, z));
                positions.Add(new Point3D(x, 0, z));
                normals.Add(new Vector3D(x, 0, z));
            }

            for (int i = 0; i < segments; i++)
            {
                int t1 = 2 + i * 2;
                int t2 = 2 + ((i + 1) % segments) * 2;
                int b1 = t1 + 1;
                int b2 = t2 + 1;

                // Top face triangle
                indices.Add(0); indices.Add(t1); indices.Add(t2);
                // Bottom face triangle
                indices.Add(1); indices.Add(b2); indices.Add(b1);
                // Side faces (two triangles)
                indices.Add(t1); indices.Add(b1); indices.Add(b2);
                indices.Add(t1); indices.Add(b2); indices.Add(t2);
            }

            return new MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                TriangleIndices = indices
            };
        }

        private MeshGeometry3D CreatePrism(int sides, double diameter, double height)
        {
            double radius = diameter / 2;
            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            // Center top (0) and bottom (1)
            positions.Add(new Point3D(0, height, 0));
            positions.Add(new Point3D(0, 0, 0));
            normals.Add(new Vector3D(0, 1, 0));
            normals.Add(new Vector3D(0, -1, 0));

            // Side vertices
            for (int i = 0; i < sides; i++)
            {
                double angle = 2 * System.Math.PI * i / sides - System.Math.PI / 2;
                double x = radius * System.Math.Cos(angle);
                double z = radius * System.Math.Sin(angle);
                positions.Add(new Point3D(x, height, z));
                normals.Add(new Vector3D(System.Math.Cos(angle), 0, System.Math.Sin(angle)));
                positions.Add(new Point3D(x, 0, z));
                normals.Add(new Vector3D(System.Math.Cos(angle), 0, System.Math.Sin(angle)));
            }

            for (int i = 0; i < sides; i++)
            {
                int t1 = 2 + i * 2;
                int t2 = 2 + ((i + 1) % sides) * 2;
                int b1 = t1 + 1;
                int b2 = t2 + 1;

                indices.Add(0); indices.Add(t1); indices.Add(t2);
                indices.Add(1); indices.Add(b2); indices.Add(b1);
                indices.Add(t1); indices.Add(b1); indices.Add(b2);
                indices.Add(t1); indices.Add(b2); indices.Add(t2);
            }

            return new MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                TriangleIndices = indices
            };
        }

        private MeshGeometry3D CreateBox(double width, double height, double depth)
        {
            double hw = width / 2, hd = depth / 2;
            var positions = new Point3DCollection
            {
                new Point3D(-hw, 0, -hd),    // 0: bottom front left
                new Point3D(hw, 0, -hd),     // 1: bottom front right
                new Point3D(hw, height, -hd), // 2: top front right
                new Point3D(-hw, height, -hd),// 3: top front left
                new Point3D(-hw, 0, hd),     // 4: bottom back left
                new Point3D(hw, 0, hd),      // 5: bottom back right
                new Point3D(hw, height, hd), // 6: top back right
                new Point3D(-hw, height, hd),// 7: top back left
            };
            var indices = new Int32Collection
            {
                // Front
                0,2,1, 0,3,2,
                // Back
                4,5,6, 4,6,7,
                // Top
                3,7,6, 3,6,2,
                // Bottom
                0,1,5, 0,5,4,
                // Left
                0,4,7, 0,7,3,
                // Right
                1,2,6, 1,6,5,
            };
            var normals = new Vector3DCollection
            {
                new Vector3D(0, 0, -1), new Vector3D(0, 0, -1),
                new Vector3D(0, 0, -1), new Vector3D(0, 0, -1),
                new Vector3D(0, 0, 1),  new Vector3D(0, 0, 1),
                new Vector3D(0, 0, 1),  new Vector3D(0, 0, 1),
                new Vector3D(0, 1, 0),  new Vector3D(0, 1, 0),
                new Vector3D(0, 1, 0),  new Vector3D(0, 1, 0),
                new Vector3D(0, -1, 0), new Vector3D(0, -1, 0),
                new Vector3D(0, -1, 0), new Vector3D(0, -1, 0),
                new Vector3D(-1, 0, 0), new Vector3D(-1, 0, 0),
                new Vector3D(-1, 0, 0), new Vector3D(-1, 0, 0),
                new Vector3D(1, 0, 0),  new Vector3D(1, 0, 0),
                new Vector3D(1, 0, 0),  new Vector3D(1, 0, 0),
            };

            return new MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                TriangleIndices = indices
            };
        }

        private void AddWireframe(Model3DGroup group, FeatureList features, ref double maxDim)
        {
            // Add thin edge lines for visual clarity
            var lineMat = new DiffuseMaterial(new SolidColorBrush(
                Color.FromRgb(50, 50, 50)));

            double offsetY = 0;
            foreach (var feature in features.Features)
            {
                if (feature is ExtrudeFeature ef)
                {
                    // Draw top/bottom circle outline
                    double r = GetProfileSize(ef.Profile) / 2;
                    AddCircleLine(group, lineMat, r, offsetY, 32);
                    AddCircleLine(group, lineMat, r, offsetY + ef.Depth, 32);
                    offsetY += ef.Depth;
                }
            }
        }

        private void AddCircleLine(Model3DGroup group, Material mat,
            double radius, double y, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                double a1 = 2 * System.Math.PI * i / segments;
                double a2 = 2 * System.Math.PI * (i + 1) / segments;
                var p1 = new Point3D(radius * System.Math.Cos(a1), y, radius * System.Math.Sin(a1));
                var p2 = new Point3D(radius * System.Math.Cos(a2), y, radius * System.Math.Sin(a2));
                var line = new MeshGeometry3D();
                line.Positions.Add(p1);
                line.Positions.Add(p2);
                line.Positions.Add(new Point3D(p1.X, p1.Y + 0.1, p1.Z));
                line.TriangleIndices.Add(0); line.TriangleIndices.Add(1); line.TriangleIndices.Add(2);
                line.Normals.Add(new Vector3D(0, 1, 0));
                line.Normals.Add(new Vector3D(0, 1, 0));
                line.Normals.Add(new Vector3D(0, 1, 0));
                group.Children.Add(new GeometryModel3D(line, mat));
            }
        }

        // === Mouse Rotation ===

        private Point _lastMousePos;
        private bool _isDragging;

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(sender as IInputElement);
            ((UIElement)sender).CaptureMouse();
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            var viewport = sender as Viewport3D;
            if (viewport?.Camera is not PerspectiveCamera cam) return;

            var pos = e.GetPosition(sender as IInputElement);
            double dx = pos.X - _lastMousePos.X;
            double dy = pos.Y - _lastMousePos.Y;
            _lastMousePos = pos;

            // Rotate camera around origin
            double sensitivity = 0.3 * _sceneScale / 100.0;
            double theta = dx * sensitivity;
            double phi = dy * sensitivity;

            var offset = cam.Position;
            // Rotate around Y axis
            double cosT = System.Math.Cos(theta), sinT = System.Math.Sin(theta);
            double newX = offset.X * cosT - offset.Z * sinT;
            double newZ = offset.X * sinT + offset.Z * cosT;
            // Rotate around X axis (tilt)
            double cosP = System.Math.Cos(phi), sinP = System.Math.Sin(phi);
            double newY = offset.Y * cosP - newZ * sinP;
            newZ = offset.Y * sinP + newZ * cosP;

            cam.Position = new Point3D(newX, newY, newZ);
            cam.LookDirection = new Vector3D(-newX, -newY, -newZ);
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }
    }
}
