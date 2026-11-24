namespace Nitrox.Model.DataStructures.Unity
{
    [TestClass]
    public class NitroxTransformTest
    {
        private const float TOLERANCE = 0.005f;

        private static readonly NitroxTransform root = new NitroxTransform(new NitroxVector3(1, 1, 1), NitroxQuaternion.FromEuler(0, 0, 0), new NitroxVector3(1, 1, 1));
        private static readonly NitroxTransform child1 = new NitroxTransform(new NitroxVector3(5, 3, -6), NitroxQuaternion.FromEuler(30, 0, 10), new NitroxVector3(2, 2, 2));
        private static readonly NitroxTransform child2 = new NitroxTransform(new NitroxVector3(11, 0, -0.5f), NitroxQuaternion.FromEuler(180, 30, 80), new NitroxVector3(0.5f, 0.5f, 0.5f));
        private static readonly NitroxTransform grandchild1 = new NitroxTransform(new NitroxVector3(13, 8, 15), NitroxQuaternion.FromEuler(-50, 5, 10), new NitroxVector3(1, 1, 1));
        private static readonly NitroxTransform grandchild2 = new NitroxTransform(new NitroxVector3(3, 18, -5), NitroxQuaternion.FromEuler(-5, 15, 1), new NitroxVector3(10, 10, 10));

        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            child1.SetParent(root, false);
            child2.SetParent(root, false);
            grandchild1.SetParent(child1, false);
            grandchild2.SetParent(child2, false);
        }

        [TestMethod]
        public void PositionTest()
        {
            NitroxVector3 rootResult = new NitroxVector3(1, 1, 1);
            NitroxVector3 child1Result = new NitroxVector3(6, 4, -5);
            NitroxVector3 child2Result = new NitroxVector3(12, 1, 0.5f);
            NitroxVector3 grandchild1Result = new NitroxVector3(28.82663f, 6.55587f, 31.11665f);
            NitroxVector3 grandchild2Result = new NitroxVector3(5.79976f, -2.040047f, 6.966463f);

            root.Position.Equals(rootResult, TOLERANCE).Should().BeTrue($"Expected: {rootResult} Found: {root.Position}");
            child1.Position.Equals(child1Result, TOLERANCE).Should().BeTrue($"Expected: {child1Result} Found: {child1.Position}");
            child2.Position.Equals(child2Result, TOLERANCE).Should().BeTrue($"Expected: {child2Result} Found: {child2.Position}");
            grandchild1.Position.Equals(grandchild1Result, TOLERANCE).Should().BeTrue($"Expected: {grandchild1Result} Found: {grandchild1.Position}");
            grandchild2.Position.Equals(grandchild2Result, TOLERANCE).Should().BeTrue($"Expected: {grandchild2Result} Found: {grandchild2.Position}");
        }

        [TestMethod]
        public void RotationTest()
        {
            NitroxQuaternion rootResult = NitroxQuaternion.FromEuler(0, 0, 0);
            NitroxQuaternion child1Result = NitroxQuaternion.FromEuler(30, 0, 10);
            NitroxQuaternion child2Result = NitroxQuaternion.FromEuler(8.537737e-07f, 210, 260);
            NitroxQuaternion grandchild1Result = NitroxQuaternion.FromEuler(340.0263f, 355.2486f, 21.87437f);
            NitroxQuaternion grandchild2Result = NitroxQuaternion.FromEuler(15.60783f, 212.4433f, 261.9936f);

            root.Rotation.Equals(rootResult, TOLERANCE).Should().BeTrue($"Expected: {rootResult} Found: {root.Rotation}");
            child1.Rotation.Equals(child1Result, TOLERANCE).Should().BeTrue($"Expected: {child1Result} Found: {child1.Rotation}");
            child2.Rotation.Equals(child2Result, TOLERANCE).Should().BeTrue($"Expected: {child2Result} Found: {child2.Rotation}");
            grandchild1.Rotation.Equals(grandchild1Result, TOLERANCE).Should().BeTrue($"Expected: {grandchild1Result} Found: {grandchild1.Rotation}");
            grandchild2.Rotation.Equals(grandchild2Result, TOLERANCE).Should().BeTrue($"Expected: {grandchild2Result} Found: {grandchild2.Rotation}");
        }

        [TestMethod]
        public void ChangingTransformTest()
        {
            NitroxVector3 beforeLocalPosition = new NitroxVector3(15, 2, -7);
            NitroxQuaternion beforeRotation = NitroxQuaternion.FromEuler(45, 15, 1);

            NitroxVector3 beforeChildLocalPosition = new NitroxVector3(75, -10, 13.333f);
            NitroxQuaternion beforeChildRotation = NitroxQuaternion.FromEuler(75, 11, 5);

            NitroxVector3 setGlobalPosition = new NitroxVector3(34.62131f, 45.99337f, -10.77733f);
            NitroxQuaternion setGlobalRotation = new NitroxQuaternion(0.6743798f, 0.2302919f, 0.02638498f, 0.7010573f);

            NitroxVector3 afterLocalPosition = new NitroxVector3(17, 14, -13);
            NitroxQuaternion afterLocalRotation = NitroxQuaternion.FromEuler(60, 25, 0);

            NitroxVector3 afterChildGlobalPosition = new NitroxVector3(379.541f, 109.6675f, -167.4466f);
            NitroxQuaternion afterChildGlobalRotation = NitroxQuaternion.FromEuler(17.81689f, 187.7957f, 151.9425f);

            NitroxTransform grandchild3 = new NitroxTransform(beforeLocalPosition, beforeRotation, new NitroxVector3(2.5f, 2.5f, 2.5f));
            NitroxTransform grandgrandchild1 = new NitroxTransform(beforeChildLocalPosition, beforeChildRotation, new NitroxVector3(1, 1, 1));

            grandgrandchild1.SetParent(grandchild3, false);
            grandchild3.SetParent(child1, true);

            grandchild3.Position.Equals(beforeLocalPosition, TOLERANCE).Should().BeTrue($"Expected: {beforeLocalPosition} Found: {grandchild3.Position}");
            grandchild3.Rotation.Equals(beforeRotation, TOLERANCE).Should().BeTrue($"Expected: {beforeRotation} Found: {grandchild3.Rotation}");

            grandchild3.Position = setGlobalPosition;
            grandchild3.Rotation = setGlobalRotation;

            grandchild3.LocalPosition.Equals(afterLocalPosition, TOLERANCE).Should().BeTrue($"Expected: {afterLocalPosition} Found: {grandchild3.LocalPosition}");
            grandchild3.LocalRotation.Equals(afterLocalRotation, TOLERANCE).Should().BeTrue($"Expected: {afterLocalRotation} Found: {grandchild3.LocalRotation}");

            grandgrandchild1.Position.Equals(afterChildGlobalPosition, TOLERANCE).Should().BeTrue($"Expected: {afterChildGlobalPosition} Found: {grandgrandchild1.Position}");
            grandgrandchild1.Rotation.Equals(afterChildGlobalRotation, TOLERANCE).Should().BeTrue($"Expected: {afterChildGlobalRotation} Found: {grandgrandchild1.Rotation}");
        }

        [TestMethod]
        public void CameraLoadedUnderItsRoomKeepsAFinitePlaceWhenItIsMovedAfterLeavingItsDock()
        {
            // As the test world was saved and loaded: a base, a Scanner Room whose transform is empty, and a docked
            // camera whose transform holds a place in the world. The loader links every child to its parent's transform.
            (NitroxTransform _, NitroxTransform _, NitroxTransform camera) = LoadedScannerRoomHierarchy();
            NitroxVector3 reported = new(-215f, -65f, 127f);

            // The camera leaves its dock (only the entity tree changes) and its simulating player reports where it is.
            camera.Position = reported;
            camera.Rotation = NitroxQuaternion.Identity;

            camera.IsFinite().Should().BeTrue($"the transform was {camera}");
            camera.Position.Equals(reported, TOLERANCE).Should().BeTrue($"Expected: {reported} Found: {camera.Position}");
        }

        [TestMethod]
        public void CameraLoadedUnderItsRoomKeepsItsPlaceWhenItIsTakenOutOfTheRoom()
        {
            (NitroxTransform _, NitroxTransform _, NitroxTransform camera) = LoadedScannerRoomHierarchy();
            NitroxVector3 saved = camera.LocalPosition;

            // What EntityRegistry.RemoveFromParent does for any entity that leaves its parent.
            camera.SetParent(null, true);

            camera.IsFinite().Should().BeTrue($"the transform was {camera}");
            camera.Position.Equals(saved, TOLERANCE).Should().BeTrue($"Expected: {saved} Found: {camera.Position}");
        }

        [TestMethod]
        public void ChildOfAParentWithoutSizeKeepsItsLastPlaceInsteadOfTakingOneThatIsNotANumber()
        {
            NitroxTransform flat = new(new NitroxVector3(10, 0, 0), NitroxQuaternion.Identity, new NitroxVector3(0, 1, 1));
            NitroxTransform child = new(new NitroxVector3(1, 2, 3), NitroxQuaternion.Identity, NitroxVector3.One);
            child.SetParent(flat, false);

            child.Position = new NitroxVector3(50, 60, 70);
            child.Rotation = NitroxQuaternion.FromEuler(0, 90, 0);
            child.SetParent(null, true);

            child.IsFinite().Should().BeTrue($"the transform was {child}");
        }

        [TestMethod]
        public void TransformWithANumberThatIsNotFiniteIsNotFinite()
        {
            new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One).IsFinite().Should().BeTrue();
            new NitroxTransform(new NitroxVector3(float.NaN, 0, 0), NitroxQuaternion.Identity, NitroxVector3.One).IsFinite().Should().BeFalse();
            new NitroxTransform(NitroxVector3.Zero, new NitroxQuaternion(0, float.NaN, 0, 1), NitroxVector3.One).IsFinite().Should().BeFalse();
            new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, new NitroxVector3(1, float.PositiveInfinity, 1)).IsFinite().Should().BeFalse();
        }

        /// <summary>
        ///     Base 858c4b56, room 9b3ce5a6 and camera 2 as the test world of 2026-11-24 saved them.
        /// </summary>
        private static (NitroxTransform Base, NitroxTransform Room, NitroxTransform Camera) LoadedScannerRoomHierarchy()
        {
            NitroxTransform baseTransform = new(new NitroxVector3(-152.28033f, -15.6695175f, 99.544716f), new NitroxQuaternion(0f, -0.8469493f, 0f, 0.53167367f), NitroxVector3.One);
            NitroxTransform room = new();
            NitroxTransform camera = new(new NitroxVector3(-142.92966f, -14.782113f, 105.73807f), new NitroxQuaternion(0f, 0.97483367f, 0f, 0.22293334f), NitroxVector3.One);
            room.SetParent(baseTransform, false);
            camera.SetParent(room, false);
            return (baseTransform, room, camera);
        }
    }
}
