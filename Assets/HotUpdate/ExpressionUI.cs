using UnityEngine;
using UnityEngine.UI;
using Symbiosis.Services;

namespace Symbiosis.UI
{
    public class ExpressionUI : MonoBehaviour
    {
        [Header("表情显示")]
        public Image expressionImage;

        [Header("表情资源（5 组）")]
        public Sprite excited;
        public Sprite happy;
        public Sprite calm;
        public Sprite sad;
        public Sprite angry;

        private string _currentExpression;

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            string expr = gm.Expression;
            if (expr != _currentExpression)
            {
                _currentExpression = expr;
                UpdateExpression(expr);
            }
        }

        private void UpdateExpression(string expression)
        {
            Sprite target = calm;
            if (expression == "expr_excited") target = excited;
            else if (expression == "expr_happy") target = happy;
            else if (expression == "expr_sad") target = sad;
            else if (expression == "expr_angry") target = angry;

            if (expressionImage != null && target != null)
                expressionImage.sprite = target;
        }
    }
}
