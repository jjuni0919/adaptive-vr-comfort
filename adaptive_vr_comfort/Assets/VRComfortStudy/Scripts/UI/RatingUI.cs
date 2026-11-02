using UnityEngine;

namespace VRComfortStudy
{
    public sealed class RatingUI : MonoBehaviour
    {
        [Tooltip("Keep this component outside the panel that it hides.")]
        [SerializeField] private GameObject panel;
        public StringEvent onScoreTextChanged = new StringEvent();
        public BoolEvent onCanConfirmChanged = new BoolEvent();
        public BoolEvent onVisibilityChanged = new BoolEvent();
        private ExperimentManager manager;
        private string requestId;
        public int SelectedScore { get; private set; } = -1;
        public bool IsOpen => !string.IsNullOrEmpty(requestId);

        public void Show(ExperimentManager owner, string id)
        {
            manager = owner;
            requestId = id;
            SelectedScore = -1;
            if (panel != null && panel != gameObject) panel.SetActive(true);
            onScoreTextChanged.Invoke("점수를 선택하세요 (0~10)");
            onCanConfirmChanged.Invoke(false);
            onVisibilityChanged.Invoke(true);
        }

        public void SelectScore(int score)
        {
            if (!IsOpen || score < 0 || score > 10) return;
            SelectedScore = score;
            onScoreTextChanged.Invoke(score.ToString());
            onCanConfirmChanged.Invoke(true);
        }

        public void SelectSliderScore(float score) => SelectScore(Mathf.RoundToInt(score));

        public void Confirm()
        {
            if (manager != null && SelectedScore >= 0 && IsOpen)
                manager.SubmitRating(requestId, SelectedScore);
        }

        public void Skip()
        {
            if (manager != null && IsOpen) manager.SkipRating("USER_SKIPPED");
        }

        public void Hide()
        {
            requestId = null;
            SelectedScore = -1;
            if (panel != null && panel != gameObject) panel.SetActive(false);
            onCanConfirmChanged.Invoke(false);
            onVisibilityChanged.Invoke(false);
        }

        private void Start() { if (!IsOpen) Hide(); }
    }
}
