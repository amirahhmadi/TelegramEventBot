using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

class Program
{
    private static string token;
    private static TelegramBotClient botClient;
    static async Task Main(string[] args)
    {
        // بارگذاری پیکربندی از فایل appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // خواندن توکن از فایل پیکربندی
        token = configuration["TelegramBot:Token"];
        botClient = new TelegramBotClient(token);

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"ربات آماده است: {me.Username}");

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync
        );

        Console.ReadLine(); // ربات روشن می‌مونه
    }

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message && update.Type != UpdateType.CallbackQuery)
            return;

        var message = update.Message;
        if (message == null && update.CallbackQuery == null)
            return;

        var botUser = await bot.GetMeAsync();

        // جلوگیری از پاسخ به پیام‌های خود ربات
        if (message?.From?.Id == botUser.Id || update.CallbackQuery?.From?.Id == botUser.Id)
            return;

        var chatId = message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id; // دریافت chatId از پیام یا callbackQuery

        // ارسال دکمه اینلاین هنگام استارت
        if (message != null && message.Text != null && message.Text.ToLower() == "/start")
        {
            await SendTodayButton(chatId.Value); // ارسال دکمه برای چت کاربر
        }

        // چک کردن دستور /today
        if (message != null && message.Text != null && message.Text.ToLower() == "/today")
        {
            var keyboard = new InlineKeyboardMarkup(new[] {
                new[] {
                    InlineKeyboardButton.WithCallbackData("مناسبت‌های امروز", "today_events")
                }
            });

            await bot.SendTextMessageAsync(chatId, "برای مشاهده مناسبت‌های امروز، دکمه زیر را فشار دهید:", replyMarkup: keyboard, cancellationToken: cancellationToken);
        }

        // بررسی کلیک روی دکمه "مناسبت‌های امروز"
        else if (update.CallbackQuery?.Data == "today_events")
        {
            var date = DateTime.Now;
            var persianDate = ToPersianDate(date);
            var events = await GetTodayEvents();

            string response = $"📅 امروز {persianDate} هست.\n\n🔹 مناسبت‌ها:\n{events}";
            await bot.SendTextMessageAsync(chatId, response, cancellationToken: cancellationToken);
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"❌ خطا: {exception.Message}");
        return Task.CompletedTask;
    }

    static async Task SendTodayButton(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[] {
            new[] {
                InlineKeyboardButton.WithCallbackData("مناسبت‌های امروز", "today_events")
            }
        });

        // ارسال پیام اولیه با دکمه اینلاین
        await botClient.SendTextMessageAsync(chatId, "سلام! برای مشاهده مناسبت‌های امروز، دکمه زیر را فشار دهید:", replyMarkup: keyboard);
    }

    static string ToPersianDate(DateTime date)
    {
        var pc = new PersianCalendar();
        return $"{pc.GetYear(date)}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00}";
    }

    static async Task<string> GetTodayEvents()
    {
        var today = DateTime.Now;
        var pc = new PersianCalendar();
        int month = pc.GetMonth(today);
        int day = pc.GetDayOfMonth(today);
        int year = pc.GetYear(today);

        // URL مربوط به API برای گرفتن مناسبت‌های جلالی (بدون نیاز به API Key)
        var url = $"https://holidayapi.ir/jalali/{year}/{month}/{day}";

        try
        {
            using (var http = new HttpClient())
            {
                var response = await http.GetAsync(url);

                // بررسی وضعیت پاسخ HTTP
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    // چاپ محتویات پاسخ دریافتی
                    Console.WriteLine("Response: " + jsonResponse);

                    var data = JsonConvert.DeserializeObject<ApiResponse>(jsonResponse);

                    // بررسی اینکه آیا داده‌ها به درستی از API دریافت شده‌اند
                    if (data?.Events == null || data.Events.Count == 0)
                    {
                        return "مناسبتی برای امروز پیدا نشد.";
                    }

                    // اضافه کردن همه رویدادها (چه تعطیلات و چه غیر تعطیلات)
                    var events = string.Join("\n", data.Events.ConvertAll(e => $"▪ {e.Description}"));

                    return events;
                }
                else
                {
                    return $"خطا در دریافت داده‌ها از API. وضعیت: {response.StatusCode}";
                }
            }
        }
        catch (Exception ex)
        {
            return $"خطای داخلی: {ex.Message}";
        }
    }


}