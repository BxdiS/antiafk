import time
import random
import math
import win32gui
import win32con
import win32api

# ==================== НАСТРОЙКИ ====================
BUTTONS = [
    (133, 133), (153, 183), (156, 227), (130, 273), (136, 318),
    (114, 365), (120, 410), (133, 452), (121, 496), (150, 544), (170, 590)
]

AD_ZONE_X1, AD_ZONE_Y1 = 314, 125
AD_ZONE_X2, AD_ZONE_Y2 = 1881, 988

CENTER_X, CENTER_Y = 960, 540
ICON_X, ICON_Y = 1224, 167


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
    """Шлет нажатие клавиши с ОБЯЗАТЕЛЬНЫМ скан-кодом для DirectX движка"""
    # Вычисляем железный скан-код клавиши (для W это будет 0x11)
    scan_code = win32api.MapVirtualKey(vk_code, 0)

    # Зажимаем клавишу
    win32api.keybd_event(vk_code, scan_code, 0, 0)
    time.sleep(duration)
    # Отпускаем клавишу
    win32api.keybd_event(vk_code, scan_code, win32con.KEYEVENTF_KEYUP, 0)


def main():
    gta_hwnd, title = find_gta_hwnd()
    if not gta_hwnd:
        print("Окно игры не найдено!")
        return

    print(f"Успешно подключено к: {title}")
    print("[*] Скрипт запущен. Исправление скан-кодов DirectX применено.")
    last_idx = -1

    while True:
        if not win32gui.IsWindow(gta_hwnd):
            print("[-] Игра закрылась. Остановка.")
            break

        is_in_ad = False

        # --- ШАГ 1: Клик по категории маркетплейса в фоне ---
        idx = random.choice([i for i in range(len(BUTTONS)) if i != last_idx])
        last_idx = idx
        print(f"\n[*] [Фон] Переход в категорию №{idx + 1}")
        move_and_click_background(gta_hwnd, BUTTONS[idx][0], BUTTONS[idx][1])

        wait_before_ad = random.uniform(25.0, 30.0)
        print(f"[~] Ждем {wait_before_ad:.2f} сек. на загрузку категории...")
        time.sleep(wait_before_ad)

        # --- ШАГ 2: Клик по случайному объявлению в фоне ---
        ad_x = random.randint(AD_ZONE_X1, AD_ZONE_X2)
        ad_y = random.randint(AD_ZONE_Y1, AD_ZONE_Y2)
        print(f"[*] [Фон] Заход в случайное объявление: ({ad_x}, {ad_y})")
        move_and_click_background(gta_hwnd, ad_x, ad_y)

        is_in_ad = True

        wait_inside_ad = random.uniform(25.0, 30.0)
        print(f"[~] Ждем {wait_inside_ad:.2f} сек. внутри объявления...")
        time.sleep(wait_inside_ad)

        # --- ШАГ 3: Захват фокуса игры ---
        print("\n[!] Переключаю фокус на игру...")
        user_hwnd = win32gui.GetForegroundWindow()
        force_foreground(gta_hwnd)
        time.sleep(0.6)

        # --- ШАГ 4: Умный выход через Управление ESCAPE ---
        if is_in_ad:
            print("[*] [Активно] Обнаружено открытое объявление! Жмем 1-й ESC для выхода из него...")
            send_key_active(win32con.VK_ESCAPE, random.uniform(0.1, 0.15))
            time.sleep(random.uniform(0.5, 0.8))

        print("[*] [Активно] Закрываем маркетплейс и телефон (ESC x2)...")
        send_key_active(win32con.VK_ESCAPE, random.uniform(0.1, 0.15))
        time.sleep(random.uniform(0.4, 0.7))
        send_key_active(win32con.VK_ESCAPE, random.uniform(0.1, 0.15))
        time.sleep(1.0)

        # --- ШАГ 5: Физическая ходьба (W) с рабочим скан-кодом ---
        walk_time = random.uniform(1.5, 2.5)
        print(f"[*] [Активно] Персонаж идет вперед {round(walk_time, 2)} сек...")
        send_key_active(0x57, walk_time)  # 0x57 — виртуальный код 'W'
        time.sleep(1.2)

        # --- ШАГ 6: Возврат в маркетплейс по цепочке действий ---
        print("[*] [Активно] Достаем девайс (Стрелка Вниз)...")
        send_key_active(win32con.VK_DOWN, random.uniform(0.1, 0.15))
        time.sleep(random.uniform(1.2, 1.6))

        print(f"[*] [Активно] Клик по центру экрана: ({CENTER_X}, {CENTER_Y})...")
        move_and_click_background(gta_hwnd, CENTER_X, CENTER_Y)
        time.sleep(random.uniform(0.8, 1.2))

        print(f"[*] [Активно] Открываем приложение маркетплейса: ({ICON_X}, {ICON_Y})...")
        move_and_click_background(gta_hwnd, ICON_X, ICON_Y)
        time.sleep(2.0)

        is_in_ad = False

        # --- ШАГ 7: Возврат фокуса пользователю ---
        if user_hwnd and user_hwnd != gta_hwnd:
            print("[+] Возвращаю фокус на твое рабочее окно.")
            force_foreground(user_hwnd)

        # --- ШАГ 8: Большой перерыв ---
        main_sleep = random.uniform(180.0, 360.0)
        print(f"[Zzz] Цикл завершен. Следующая проверка через {round(main_sleep / 60, 2)} мин...")
        time.sleep(main_sleep)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n[-] Скрипт остановлен.")