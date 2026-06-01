import time
import random
import math
import win32gui
import win32con
import win32api

# Твой список координат
BUTTONS = [
    (133, 133), (153, 183), (156, 227), (130, 273), (136, 318),
    (114, 365), (120, 410), (133, 452), (121, 496), (150, 544), (170, 590)
]


def find_gta_hwnd():
    # Поиск по названию
    target_titles = ["Grand Theft Auto V", "Majestic Multiplayer"]
    for title in target_titles:
        hwnd = win32gui.FindWindow(None, title)
        if hwnd:
            return hwnd, title

    # Поиск через перебор, если прямое имя не сработало
    found_windows = []

    def callback(hwnd, _):  # '_' показывает, что параметр не используется
        if win32gui.IsWindowVisible(hwnd):
            title = win32gui.GetWindowText(hwnd)
            if "Grand Theft Auto" in title or "Majestic" in title:
                found_windows.append((hwnd, title))
        return True

    win32gui.EnumWindows(callback, None)
    return found_windows[0] if found_windows else (None, None)


def move_and_click_background(hwnd, target_x, target_y):
    # Начальная точка
    start_x, start_y = random.randint(100, 1800), random.randint(100, 900)
    steps = random.randint(15, 25)

    for i in range(steps):
        t = math.sin((i / float(steps - 1)) * math.pi / 2)
        curr_x = int(start_x + (target_x - start_x) * t)
        curr_y = int(start_y + (target_y - start_y) * t)

        # WinAPI отправка перемещения
        win32gui.PostMessage(hwnd, win32con.WM_MOUSEMOVE, 0, win32api.MAKELONG(curr_x, curr_y))
        time.sleep(0.01)

    final_lparam = win32api.MAKELONG(target_x, target_y)
    win32gui.PostMessage(hwnd, win32con.WM_LBUTTONDOWN, win32con.MK_LBUTTON, final_lparam)
    time.sleep(0.1)
    win32gui.PostMessage(hwnd, win32con.WM_LBUTTONUP, 0, final_lparam)


def main():
    hwnd, title = find_gta_hwnd()
    if not hwnd:
        print("Окно не найдено!")
        return

    print(f"Подключено к: {title}")
    last_idx = -1

    while True:
        idx = random.choice([i for i in range(len(BUTTONS)) if i != last_idx])
        last_idx = idx
        move_and_click_background(hwnd, BUTTONS[idx][0], BUTTONS[idx][1])
        time.sleep(random.uniform(180, 360))


if __name__ == "__main__":
    main()