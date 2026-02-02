from PIL import Image
import os

def split_icons(input_path, output_dir):
    img = Image.open(input_path)
    # Ensure it's square-ish or at least has 4 parts
    w, h = img.size
    half_w = w // 2
    half_h = h // 2
    
    # Save, Exit, Options, Help
    boxes = [
        (0, 0, half_w, half_h),         # Save
        (half_w, 0, w, half_h),         # Exit
        (0, half_h, half_w, h),         # Options
        (half_w, half_h, w, h)          # Help
    ]
    
    # We want them to be 16x16 or 24x24 for the menu
    # The generated icons look detailed, resizing to 16x16 might lose clarity
    # but 24x24 is good. Let's do 16x16 for classic look.
    size = (16, 16)
    
    names = ["ico_save.png", "ico_exit.png", "ico_options.png", "ico_help.png"]
    
    for box, name in zip(boxes, names):
        icon = img.crop(box)
        # Find the actual icon within the quadrant (remove white space)
        # Actually, just center-resize is easier for now
        icon = icon.resize(size, Image.LANCZOS)
        icon.save(os.path.join(output_dir, name))
        print(f"Saved {name}")

if __name__ == "__main__":
    split_icons(r"C:\Users\superuser\.gemini\antigravity\brain\279f810b-0356-428d-8a40-be16719d2446\menu_icons_office2000_1770003671258.png", r"c:\SOFTDEV\spyscalp\lupdate")
