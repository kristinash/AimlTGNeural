using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
// Используем расширение для старых версий или удаляем, если используем новый StartReceiving
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace NeuralNetwork1
{
    class TLGBotik
    {
        public ITelegramBotClient botik = null;
        private UpdateTLGMessages formUpdater;
        private DatasetProcessor dataset = new DatasetProcessor();
        private BaseNetwork perseptron = null;
        private AIMLBotik aimlBot = new AIMLBotik();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public TLGBotik(BaseNetwork net, UpdateTLGMessages updater)
        {
            string keyPath = "botkey.txt";
            if (!System.IO.File.Exists(keyPath))
            {
                updater("Ошибка: файл botkey.txt не найден!");
                return;
            }

            var botKey = System.IO.File.ReadAllText(keyPath).Trim();
            botik = new TelegramBotClient(botKey);
            formUpdater = updater;
            perseptron = net;
        }

        public void SetNet(BaseNetwork net)
        {
            perseptron = net;
            formUpdater("Нейросеть обновлена!");
        }

        private async Task HandleUpdateMessageAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message == null) return;
            var message = update.Message;

            if (message.Type == MessageType.Photo)
            {
                try
                {
                    formUpdater("Загрузка фото...");
                    var photoId = message.Photo.Last().FileId;
                    var fileInfo = await botik.GetFileAsync(photoId, cancellationToken);

                    using (var imageStream = new MemoryStream())
                    {
                        await botik.DownloadFileAsync(fileInfo.FilePath, imageStream, cancellationToken);
                        imageStream.Position = 0;

                        using (var img = System.Drawing.Image.FromStream(imageStream))
                        using (var bm = new System.Drawing.Bitmap(img))
                        {
                            var sample = dataset.getSample(bm);
                            if (perseptron == null)
                            {
                                await botik.SendTextMessageAsync(message.Chat.Id, "Сеть не обучена!", cancellationToken: cancellationToken);
                                return;
                            }

                            perseptron.Predict(sample);
                            string result = DatasetProcessor.LetterTypeToString(sample.recognizedClass);

                            await botik.SendTextMessageAsync(message.Chat.Id, "Я вижу цифру: " + result, cancellationToken: cancellationToken);
                            formUpdater("Распознано: " + result);
                        }
                    }
                }
                catch (Exception ex) { formUpdater("Ошибка: " + ex.Message); }
                return;
            }

            if (message.Type == MessageType.Text)
            {
                var reply = aimlBot.Talk(message.Text);
                await botik.SendTextMessageAsync(message.Chat.Id, reply, cancellationToken: cancellationToken);
                return;
            }
        }

        // Исправлено для C# 7.3 (удален рекурсивный switch)
        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            string errorMessage = exception.Message;
            if (exception is ApiRequestException apiEx)
            {
                errorMessage = string.Format("Telegram API Error: [{0}] {1}", apiEx.ErrorCode, apiEx.Message);
            }

            formUpdater(errorMessage);
            return Task.CompletedTask;
        }

        public bool Act()
        {
            if (botik == null) return false;
            try
            {
                // Для старых версий Telegram.Bot используется такой вызов:
                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = new[] { UpdateType.Message }
                };

                botik.StartReceiving(
                    HandleUpdateMessageAsync,
                    HandleErrorAsync,
                    receiverOptions,
                    cts.Token
                );

                return true;
            }
            catch { return false; }
        }

        public void Stop() { cts.Cancel(); }
    }
}