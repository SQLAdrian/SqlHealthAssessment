# -*- coding: utf-8 -*-
import base64
import requests
import sys
import os
import glob
import concurrent.futures

sys.stdout.reconfigure(encoding="utf-8")


def process_image(
    image_path,
    prompt="Analyze this UI screenshot for UX issues, focusing on layout, readability, and usability under pressure.",
):
    try:
        with open(image_path, "rb") as f:
            image_data = base64.b64encode(f.read()).decode()
    except FileNotFoundError:
        return "Image file not found."

    payload = {
        "model": "gemma-4",  # Adjust if model name differs in LMStudio
        "messages": [
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": prompt},
                    {
                        "type": "image_url",
                        "image_url": {"url": f"data:image/jpeg;base64,{image_data}"},
                    },
                ],
            }
        ],
        "max_tokens": 2000,
    }

    try:
        response = requests.post(
            "http://localhost:1234/v1/chat/completions", json=payload, timeout=30
        )
        response.raise_for_status()
        return response.json()["choices"][0]["message"]["content"]
    except requests.RequestException as e:
        return f"Request failed: {e}"


if __name__ == "__main__":
    # Parse arguments
    args = sys.argv[1:]
    prompt = "Analyze this UI screenshot for UX issues, focusing on layout, readability, and usability under pressure."
    output_file = "screenshots_review.md"
    input_path = None

    i = 0
    while i < len(args):
        if args[i] == "--prompt" and i + 1 < len(args):
            prompt = args[i + 1]
            i += 2
        elif args[i] == "--output" and i + 1 < len(args):
            output_file = args[i + 1]
            i += 2
        else:
            if input_path is None:
                input_path = args[i]
            i += 1

    if input_path is None:
        print(
            "Usage: python process_image.py <image_path> [--prompt 'custom prompt'] [--output filename] or <directory>"
        )
        sys.exit(1)

    if os.path.isfile(input_path):
        # Single file
        result = process_image(input_path, prompt)
        print(result)
    elif os.path.isdir(input_path):
        # Directory: process all JPEG
        dir_path = input_path
        jpeg_files = sorted(glob.glob(os.path.join(dir_path, "*.JPEG")))
        if not jpeg_files:
            print("No JPEG files found in directory.")
            sys.exit(1)
        output_path = os.path.join(dir_path, "..", "..", output_file)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            futures = {
                executor.submit(process_image, img, prompt): img for img in jpeg_files
            }
            results = {}
            for future in concurrent.futures.as_completed(futures):
                img = futures[future]
                results[img] = future.result()
                print(f"Processed {os.path.basename(img)}")
        with open(output_path, "w", encoding="utf-8") as f:
            f.write("# Screenshots UX Review\n\n")
            for img in jpeg_files:
                result = results[img]
                f.write(f"## {os.path.basename(img)}\n\n{result}\n\n---\n\n")
        print(f"Review saved to {output_path}")
    else:
        print("Input path not found.")
        sys.exit(1)
