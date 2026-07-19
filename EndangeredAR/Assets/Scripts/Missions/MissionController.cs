using UnityEngine;

namespace EndangeredAR.Missions
{
    public class MissionController : MonoBehaviour
    {
        public enum MissionState
        {
            NotStarted,
            Choosing,
            Correct,
            Wrong,
            Completed
        }

        [SerializeField] private string currentMissionId = "sensen_food";
        [SerializeField] private int points;
        [SerializeField] private MissionState state = MissionState.NotStarted;

        public string CurrentMissionId => currentMissionId;
        public int Points => points;
        public MissionState State => state;
        public bool IsCompleted => state == MissionState.Completed;

        public void StartFoodMission()
        {
            if (state == MissionState.Completed)
            {
                return;
            }

            state = MissionState.Choosing;
        }

        public MissionResult SelectFood(string option)
        {
            if (state == MissionState.Completed)
            {
                return new MissionResult(
                    true,
                    "你已经帮我找到适合的森林食物啦！这枚生态守护者徽章会一直记录你的行动。",
                    "缨冠灰叶猴主要吃嫩叶、果实和花朵，完整森林就是它们的食堂。"
                );
            }

            if (state != MissionState.Choosing && state != MissionState.Wrong)
            {
                StartFoodMission();
            }

            var normalized = option == null ? string.Empty : option.Trim();
            var isCorrect = normalized.Contains("嫩叶") || normalized.Contains("果实") || normalized.Contains("花朵");
            if (!isCorrect)
            {
                state = MissionState.Wrong;
                return new MissionResult(
                    false,
                    "这个不适合我吃呀。野生动物不能吃人类零食，更不能碰塑料。",
                    "森森学会了分辨不适合野生动物的食物。"
                );
            }

            state = MissionState.Completed;
            points += 20;
            return new MissionResult(
                true,
                "找到啦！嫩叶、果实和花朵都是我喜欢的森林食物，谢谢你帮我守住餐桌。",
                "缨冠灰叶猴主要吃嫩叶、果实和花朵，完整森林就是它们的食堂。"
            );
        }

        public void CompleteCurrentMission()
        {
            if (state == MissionState.Completed)
            {
                return;
            }

            state = MissionState.Completed;
            points += 20;
        }
    }

    public struct MissionResult
    {
        public MissionResult(bool success, string feedback, string learnedFact)
        {
            Success = success;
            Feedback = feedback;
            LearnedFact = learnedFact;
        }

        public bool Success { get; }
        public string Feedback { get; }
        public string LearnedFact { get; }
    }
}
