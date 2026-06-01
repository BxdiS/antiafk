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

# Координаты для детекции меню карты / паузы
MAP_PIXEL_X, MAP_PIXEL_Y = 512, 120

# Координаты для детекции состояний планшета
HUD_PIXEL = (1888, 25)
MP_PIXEL = (1770, 34)


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
                print(f"[!] ОБНАРУЖЕНО уведомление склада! Клик по ({WARN_CLICK[0]}, {WARN_CLICK[1]})...")
                pyautogui.click(WARN_CLICK[0], WARN_CLICK[1])
                time.sleep(1.0)
                return True
    return False


def check_and_close_map():
    """Проверяет, не залетели ли мы случайно в карту/меню паузы, и закрывает её"""
    img = ImageGrab.grab(bbox=(MAP_PIXEL_X, MAP_PIXEL_Y, MAP_PIXEL_X + 1, MAP_PIXEL_Y + 1))
    r, g, b = img.getpixel((0, 0))

    # Ищем розово-пурпурный цвет #F0006C
    if r > 200 and g < 40 and b > 80 and b < 140:
        print("[!] Обнаружено случайное открытие меню карты! Закрываем (ESC)...")
        send_key_active(win32con.VK_ESCAPE, 0.1)
        time.sleep(1.0)
        return True
    return False


def smart_state_recovery():
    """Анализирует пиксели и приводит интерфейс к открытому маркетплейсу"""
    print("[~] Анализ состояния интерфейса...")

    # Захват пикселя HUD в игре
    img_hud = ImageGrab.grab(bbox=(HUD_PIXEL[0], HUD_PIXEL[1], HUD_PIXEL[0] + 1, HUD_PIXEL[1] + 1))
    r_hud, g_hud, b_hud = img_hud.getpixel((0, 0))

    # Захват пикселя заголовка маркетплейса
    img_mp = ImageGrab.grab(bbox=(MP_PIXEL[0], MP_PIXEL[1], MP_PIXEL[0] + 1, MP_PIXEL[1] + 1))
    r_mp, g_mp, b_mp = img_mp.getpixel((0, 0))

    # СОСТОЯНИЕ 1: Маркетплейс затемнен предупреждением (#1E416A)
    # R: ~30, G: ~65, B: ~106
    if 15 <= r_mp <= 50 and 45 <= g_mp <= 90 and 85 <= b_mp <= 130:
        print("[*] Статус: Маркетплейс открыт, но перекрыт плашкой. Убираем плашку...")
        check_and_close_warning()
        return

    # СОСТОЯНИЕ 2: Чистый активный маркетплейс (#3D82D5)
    # R: ~61, G: ~130, B: ~213
    if 40 <= r_mp <= 85 and 110 <= g_mp <= 160 and 190 <= b_mp <= 245:
        print("[*] Статус: Маркетплейс активен. Все отлично.")
        return

    # СОСТОЯНИЕ 3: Просто в игре, горит HUD (#FF007F)
    # R: 255, G: 0, B: 127
    if r_hud >= 200 and g_hud <= 60 and 80 <= b_hud <= 170:
        print("[*] Статус: Персонаж в игре. Открываю планшет и маркетплейс...")
        send_key_active(win32con.VK_DOWN, 0.1)
        time.sleep(1.5)
        pyautogui.click(CENTER_X, CENTER_Y)
        time.sleep(1.0)
        pyautogui.click(ICON_X, ICON_Y)
        time.sleep(4.5)  # Ждем загрузки
        check_and_close_warning()
        return

    # ЕСЛИ СОСТОЯНИЕ НЕИЗВЕСТНО (Например, другая менюшка или темнота)
    print(
        f"[?] Статус: Неопознан (HUD: {r_hud},{g_hud},{b_hud} | MP: {r_mp},{g_mp},{b_mp}). Пробую дефолтное открытие...")
    send_key_active(win32con.VK_DOWN, 0.1)
    time.sleep(1.5)
    pyautogui.click(CENTER_X, CENTER_Y)
    time.sleep(1.0)
    pyautogui.click(ICON_X, ICON_Y)
    time.sleep(4.5)
    check_and_close_warning()


def move_and_click_background(gta_hwnd, target_x, target_y):
    start_x, start_y = random.randint(100, 1800), random.randint(100, 900)
    steps = random.randint(15, 25)
    for i in range(steps):
        t = math.sin((i / float(steps - 1)) * math.pi / 2)
        curr_x = int(start_x + (target_x - start_x) * t)
        curr_y = int(start_y + (target_y - start_y) * t)
        win32gui.PostMessage(gta_hwnd, win32con.WM_MOUSEMOVE, 0, win32api.MAKELONG(curr_x, curr_y))
        time.sleep(0.01)

    final_x = target_x + random.randint(-4, 4)
    final_y = target_y + random.randint(-4, 4)
    final_lparam = win32api.MAKELONG(final_x, final_y)

    win32gui.PostMessage(gta_hwnd, win32con.WM_MOUSEMOVE, 0, final_lparam)
    time.sleep(0.05)
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

    # Инициализация при старте скрипта (Проверка и открытие маркетплейса, если надо)
    print("\n[!] Первичная инициализация состояния...")
    user_hwnd = win32gui.GetForegroundWindow()
    force_foreground(gta_hwnd)
    time.sleep(1.0)
    smart_state_recovery()
    if user_hwnd:
        force_foreground(user_hwnd)

    print("[*] Скрипт перешел в рабочий цикл.\n")
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

        # Защитная проверка: если улетели в карту паузы
        check_and_close_map()

        walk_time = random.uniform(1.5, 2.5)
        print(f"[*] Иду вперед {walk_time:.2f} сек...")
        send_key_active(0x57, walk_time)
        time.sleep(1.2)

        # Вызов нашей умной функции возврата
        smart_state_recovery()

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