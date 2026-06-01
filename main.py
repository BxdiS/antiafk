import time
import random
import math
import win32gui
import win32con
import win32api
import pyautogui
from PIL import ImageGrab

# ==================== НАСТРОЙКИ ====================
BUTTONS = [
    (133, 133), (153, 183), (156, 227), (130, 273), (136, 318),
    (114, 365), (120, 410), (133, 452), (121, 496), (150, 544), (170, 590)
]

AD_ZONE_X1, AD_ZONE_Y1 = 314, 125
AD_ZONE_X2, AD_ZONE_Y2 = 1881, 988

CENTER_X, CENTER_Y = 960, 540
ICON_X, ICON_Y = 1224, 167

# Область для проверки красного цвета (770-780, 436-438)
WARN_BOX = (770, 436, 781, 439)
WARN_CLICK = (1084, 630)


# ===================================================

def find_gta_hwnd():
    target_titles = ["Grand Theft Auto V", "Majestic Multiplayer"]
    for title in target_titles:
        gta_hwnd = win32gui.FindWindow(None, title)
        if gta_hwnd:
            return gta_hwnd, title
    return None, None


def force_foreground(hwnd_target):
    try:
        win32gui.ShowWindow(hwnd_target, win32con.SW_RESTORE)
        win32gui.SetForegroundWindow(hwnd_target)
    except Exception:
        win32api.keybd_event(win32con.VK_MENU, 0, 0, 0)
        win32gui.SetForegroundWindow(hwnd_target)
        win32api.keybd_event(win32con.VK_MENU, 0, win32con.KEYEVENTF_KEYUP, 0)


def check_and_close_warning():
    """Проверяет красные пиксели и закрывает уведомление о товарах"""
    img = ImageGrab.grab(bbox=WARN_BOX)
    for x in range(img.width):
        for y in range(img.height):
            r, g, b = img.getpixel((x, y))
            # Проверка на красный цвет (с допуском)
            if r > 180 and g < 100 and b < 100:
                print(f"[!] ОБНАРУЖЕНО уведомление! Клик по ({WARN_CLICK[0]}, {WARN_CLICK[1]})...")
                pyautogui.click(WARN_CLICK[0], WARN_CLICK[1])
                time.sleep(1.0)
                return True
    return False


def move_and_click_background(gta_hwnd, target_x, target_y):
    start_x, start_y = random.randint(100, 1800), random.randint(100, 900)
    steps = random.randint(15, 25)
    for i in range(steps):
        t = math.sin((i / float(steps - 1)) * math.pi / 2)
        curr_x = int(start_x + (target_x - start_x) * t)
        curr_y = int(start_y + (target_y - start_y) * t)
        win32gui.PostMessage(gta_hwnd, win32con.WM_MOUSEMOVE, 0, win32api.MAKELONG(curr_x, curr_y))
        time.sleep(0.01)

    final_lparam = win32api.MAKELONG(target_x, target_y)
    win32gui.PostMessage(gta_hwnd, win32con.WM_LBUTTONDOWN, win32con.MK_LBUTTON, final_lparam)
    time.sleep(random.uniform(0.07, 0.15))
    win32gui.PostMessage(gta_hwnd, win32con.WM_LBUTTONUP, 0, final_lparam)


def send_key_active(vk_code, duration=0.1):
    scan_code = win32api.MapVirtualKey(vk_code, 0)
    win32api.keybd_event(vk_code, scan_code, 0, 0)
    time.sleep(duration)
    win32api.keybd_event(vk_code, scan_code, win32con.KEYEVENTF_KEYUP, 0)


def main():
    gta_hwnd, title = find_gta_hwnd()
    if not gta_hwnd:
        print("[-] Окно игры не найдено!")
        return

    print(f"[+] Подключено к: {title}")
    last_idx = -1

    while True:
        if not win32gui.IsWindow(gta_hwnd):
            print("[-] Игра закрылась. Выход.")
            break

        is_in_ad = False

        # --- ФОНОВЫЕ ДЕЙСТВИЯ ---
        idx = random.choice([i for i in range(len(BUTTONS)) if i != last_idx])
        last_idx = idx
        print(f"\n[*] [Фон] Клик по категории №{idx + 1}")
        move_and_click_background(gta_hwnd, BUTTONS[idx][0], BUTTONS[idx][1])
        time.sleep(random.uniform(25, 30))

        ad_x, ad_y = random.randint(AD_ZONE_X1, AD_ZONE_X2), random.randint(AD_ZONE_Y1, AD_ZONE_Y2)
        print(f"[*] [Фон] Клик по объявлению: ({ad_x}, {ad_y})")
        move_and_click_background(gta_hwnd, ad_x, ad_y)
        is_in_ad = True
        time.sleep(random.uniform(25, 30))

        # --- АКТИВНЫЕ ДЕЙСТВИЯ ---
        print("[!] Перехват фокуса для ходьбы...")
        user_hwnd = win32gui.GetForegroundWindow()
        force_foreground(gta_hwnd)
        time.sleep(0.8)

        if is_in_ad:
            print("[*] Выход из объявления (ESC)...")
            send_key_active(win32con.VK_ESCAPE, 0.1)
            time.sleep(0.5)

        print("[*] Закрытие маркетплейса (ESC x2)...")
        send_key_active(win32con.VK_ESCAPE, 0.1)
        time.sleep(0.5)
        send_key_active(win32con.VK_ESCAPE, 0.1)
        time.sleep(1.0)

        walk_time = random.uniform(1.5, 2.5)
        print(f"[*] Иду вперед {walk_time:.2f} сек...")
        send_key_active(0x57, walk_time)
        time.sleep(1.2)

        print("[*] Возврат в маркетплейс...")
        send_key_active(win32con.VK_DOWN, 0.1)
        time.sleep(1.5)
        pyautogui.click(CENTER_X, CENTER_Y)
        time.sleep(1.0)
        pyautogui.click(ICON_X, ICON_Y)
        time.sleep(2.0)

        # Проверка склада
        check_and_close_warning()

        if user_hwnd:
            print("[+] Возврат фокуса пользователю.")
            force_foreground(user_hwnd)

        main_sleep = random.uniform(180, 360)
        print(f"[Zzz] Цикл завершен. Сплю {main_sleep / 60:.2f} мин.")
        time.sleep(main_sleep)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n[-] Скрипт остановлен пользователем.")