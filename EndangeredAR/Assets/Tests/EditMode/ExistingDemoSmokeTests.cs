using EndangeredAR.Missions;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class ExistingDemoSmokeTests
    {
        [Test]
        public void SensenFoodMission_AwardsPointsOnlyOnce()
        {
            var go = new GameObject("Mission Test");
            try
            {
                var controller = go.AddComponent<MissionController>();
                var mission = Resources.Load<MissionDefinition>("Animals/SensenMission");
                Assert.That(mission, Is.Not.Null);

                controller.Configure(mission);
                controller.StartFoodMission();
                Assert.That(controller.SelectFood("嫩叶").Success, Is.True);
                Assert.That(controller.SelectFood("花朵").Success, Is.True);
                Assert.That(controller.Points, Is.EqualTo(20));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
