using UnityEngine;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 小球标记方法枚举
    /// 定义不同的标记方式及其优先级
    /// </summary>
    public enum BallMarkingMethod
    {
        None = 0,           // 未标记
        HeadGaze = 1,       // 头盔注视标记
        ControllerPoint = 2, // 手柄指向标记  
        Manual = 3          // 手动标记
    }
    
    /// <summary>
    /// 标记方法的扩展方法
    /// </summary>
    public static class BallMarkingMethodExtensions
    {
        /// <summary>
        /// 获取标记方法对应的颜色
        /// </summary>
        public static Color GetMarkingColor(this BallMarkingMethod method)
        {
            return method switch
            {
                BallMarkingMethod.HeadGaze => Color.yellow,       // 黄色 = 注视
                BallMarkingMethod.ControllerPoint => Color.cyan,  // 青色 = 指向
                BallMarkingMethod.Manual => Color.magenta,        // 紫色 = 手动
                _ => Color.white
            };
        }
        
        /// <summary>
        /// 获取标记方法的显示名称
        /// </summary>
        public static string GetDisplayName(this BallMarkingMethod method)
        {
            return method switch
            {
                BallMarkingMethod.HeadGaze => "👀 注视",
                BallMarkingMethod.ControllerPoint => "🎮 指向",
                BallMarkingMethod.Manual => "⌨️ 手动",
                _ => "未标记"
            };
        }
    }
}
