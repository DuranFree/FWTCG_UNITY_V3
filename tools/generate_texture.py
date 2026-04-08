"""
RunningHub 文生图脚本 - 生成 Unity 贴图
用法：
  python tools/generate_texture.py "prompt内容" 输出文件名.png [分辨率] [比例]

示例：
  python tools/generate_texture.py "fantasy card game zone border, dark blue glowing, UI frame" zone_hero.png
  python tools/generate_texture.py "red fire battlefield zone frame" zone_battlefield.png 2k 1:1

参数说明：
  prompt    生成图片的描述（英文效果更好）
  filename  保存文件名，存入 Assets/Resources/UI/Generated/
  resolution  可选，1k / 2k / 4k（默认 1k）
  aspectRatio 可选，如 1:1 / 16:9 / 4:3（默认 1:1）
"""

import sys
import os
import time
import json
import requests

API_KEY = "22be73b59bc447dcaf7c407face845eb"
BASE_URL = "https://www.runninghub.cn"
MODEL_PATH = "/openapi/v2/rhart-image-n-pro/text-to-image"

# Unity 项目输出目录（相对脚本位置向上一级）
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.dirname(SCRIPT_DIR)
OUTPUT_DIR = os.path.join(PROJECT_DIR, "Assets", "Resources", "UI", "Generated")


def generate(prompt: str, filename: str, resolution: str = "1k", aspect_ratio: str = "1:1"):
    print(f"[1/4] 发起生成请求...")
    print(f"      Prompt: {prompt}")
    print(f"      尺寸: {resolution} | 比例: {aspect_ratio}")

    headers = {
        "Authorization": f"Bearer {API_KEY}",
        "Content-Type": "application/json",
    }
    body = {
        "prompt": prompt,
        "aspectRatio": aspect_ratio,
        "resolution": resolution,
    }

    resp = requests.post(BASE_URL + MODEL_PATH, headers=headers, json=body, timeout=30)
    resp.raise_for_status()
    data = resp.json()

    print(f"      响应: {json.dumps(data, ensure_ascii=False)[:300]}")

    # 提取 taskId
    task_id = None
    if isinstance(data, dict):
        task_id = (
            data.get("data", {}).get("taskId")
            or data.get("taskId")
        )
        # 如果直接返回图片 URL（同步接口）
        file_url = (
            data.get("data", {}).get("fileUrl")
            or data.get("fileUrl")
        )
        if file_url:
            print(f"[2/4] 同步接口，直接获得图片 URL")
            return download_and_save(file_url, filename)

    if not task_id:
        # 尝试旧版 task/openapi 格式
        print(f"[!] 未获得 taskId，尝试解析为列表结果...")
        if isinstance(data, list) and data:
            file_url = data[0].get("fileUrl")
            if file_url:
                return download_and_save(file_url, filename)
        raise RuntimeError(f"无法解析响应: {data}")

    print(f"[2/4] 任务已提交，taskId={task_id}")
    return poll_and_save(task_id, filename)


def poll_and_save(task_id: str, filename: str, max_wait: int = 180):
    """轮询任务状态直到完成（v2 API 用 /openapi/v2/query）"""
    print(f"[3/4] 等待生成结果（最多 {max_wait}s）...")
    headers = {
        "Authorization": f"Bearer {API_KEY}",
        "Content-Type": "application/json",
    }

    start = time.time()
    while time.time() - start < max_wait:
        time.sleep(4)
        elapsed = int(time.time() - start)

        try:
            resp = requests.post(
                BASE_URL + "/openapi/v2/query",
                headers=headers,
                json={"taskId": task_id},
                timeout=15,
            )
            result = resp.json()
        except Exception as e:
            print(f"      [{elapsed}s] 查询异常: {e}")
            continue

        print(f"      [{elapsed}s] {str(result)[:200]}")

        if not isinstance(result, dict):
            continue

        status = result.get("status", "")
        error_code = result.get("errorCode", "")

        # 失败
        if error_code and error_code not in ("", "0"):
            raise RuntimeError(f"生成失败 {error_code}: {result.get('errorMessage')}")

        # 完成：results 字段包含图片列表
        results = result.get("results")
        if results and isinstance(results, list):
            for item in results:
                url = item.get("fileUrl") or item.get("url")
                if url:
                    return download_and_save(url, filename)

        # 还在跑
        if status in ("QUEUED", "RUNNING", ""):
            continue

        # 兜底：任何包含图片 URL 的字段
        for key in ("fileUrl", "url", "imageUrl"):
            url = result.get(key)
            if url and url.startswith("http"):
                return download_and_save(url, filename)

    raise TimeoutError(f"等待超时（{max_wait}s），taskId={task_id}")


def download_and_save(file_url: str, filename: str):
    """下载图片并保存到 Unity 项目"""
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    output_path = os.path.join(OUTPUT_DIR, filename)

    print(f"[4/4] 下载图片: {file_url}")
    resp = requests.get(file_url, timeout=60)
    resp.raise_for_status()

    with open(output_path, "wb") as f:
        f.write(resp.content)

    size_kb = len(resp.content) // 1024
    print(f"\n[DONE] 完成！已保存到:")
    print(f"   {output_path}")
    print(f"   大小: {size_kb} KB")
    return output_path


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)

    prompt_text = sys.argv[1]
    output_name = sys.argv[2]
    res = sys.argv[3] if len(sys.argv) > 3 else "1k"
    ratio = sys.argv[4] if len(sys.argv) > 4 else "1:1"

    # 自动加 .png 扩展名
    if not output_name.lower().endswith((".png", ".jpg", ".jpeg", ".webp")):
        output_name += ".png"

    generate(prompt_text, output_name, res, ratio)
