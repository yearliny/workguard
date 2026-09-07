# WorkGuard 0.3.0 视觉素材

两幅装饰性插画由内置 ImageGen 工具生成，保留透明通道并直接嵌入 WPF Resource。它们是生活场景插画，不是动作教学或医学图示。未通过 Python、SVG 或 CSS 模拟插画。

| 文件 | 用途 | 解码上限 |
| --- | --- | --- |
| `src/WorkGuard.Windows/Assets/workday-pause.png` | 首页、身体活动准备页 | 440 像素宽 |
| `src/WorkGuard.Windows/Assets/window-rest.png` | 设置、轻提醒、眼睛休息准备、完成页 | 360 像素宽 |

源 PNG 一并保存在仓库。运行期共享已解码资源，不请求网络。活动进行时隐藏装饰图片。图片增加了包体积，但没有增加运行时、字体文件或 UI 框架依赖。

图标来源：Windows 系统字体，[微软 Segoe Fluent Icons](https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-fluent-icons-font) 与 [Segoe MDL2 Assets](https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-ui-symbol-font)。显式设置字体，优先 Fluent，回退 MDL2；不复制或重新分发字体文件。装饰性图标不生成自动化朗读节点，控件保留文字标签。

## 生成提示词：workday-pause.png

Use case: illustration-story. Create a finished decorative illustration asset for WorkGuard, a calm native Windows work-break app. One single square composition, ideally 512x512, truly transparent background with alpha. A friendly stylized adult office worker in a soft sage green sweater and warm apricot trousers has stepped away from a small minimal desk with closed laptop, standing comfortably looking toward a sunlit window with a leafy plant. Natural relaxed upright pose, arms relaxed, no stretch instruction. Contemporary premium editorial vector-like illustration, rounded organic silhouettes, delicate dark forest green lines, flat muted color areas with only very subtle paper texture. Palette sage #B8CDBD, forest #326958, cream #F3EBDD, apricot #E7B391, muted blue-grey accents. Compact balanced scene occupying 85 percent of canvas, easy to read at 190x190 pixels; objects fully inside canvas with clear breathing space. Warm approachable adult aesthetic, not childish or corporate stock art. No text, letters, numbers, UI, borders, watermark, medical symbols, confetti, dramatic exercise, camera or screen details. This will be integrated as a real local app asset, return the generated image file.

## 生成提示词：window-rest.png

Use case: illustration-story. A compact finished decorative illustration for the eyes-rest and quiet-time sections of a Windows desktop wellbeing app. Single square image, truly transparent alpha background, no opaque background rectangle. A small open cream window frames a distant soft sage hillside and warm apricot sun; a leafy potted plant sits on the sill, a tiny cream mug nearby. No people, no furniture room, no letters or text. Premium warm contemporary editorial illustration, rounded organic shapes, clean flat fills and restrained dark forest green outlines, avoid grain and tiny texture. Palette forest #326958, sage #B8CDBD, cream #F3EBDD, warm apricot #E7B391. Calm inviting natural daylight, aesthetically coherent with an illustration of a woman in sage sweater resting beside a desk. All shapes safely inside frame with 10 percent breathing room. Legible at 140 pixels, simple silhouette, balanced square composition. Decorative only, not a medical or exercise diagram. No UI, no border, no watermark. Ideally export compact 512x512 PNG.
