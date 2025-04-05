from fastapi import FastAPI, Request
from pydantic import BaseModel
import os
import json

import asyncio

from llama_cpp import Llama



# Папка для сохранения полученных JSON файлов
save_directory = "received_jsons"
os.makedirs(save_directory, exist_ok=True)

class TextMessage(BaseModel):
    content: str

class FilePath(BaseModel):
    path: str

class ModelHandler:
    model_path: str
    llm: Llama
    system_message: dict[str, str]
    messages: list[dict[str, str]]
    metadata = None

    system_prompt = "You are a LLM`s developer. You know them well and can provide answers on every question. You also can estimate users suggestions. You interested in creating different system prompts for LLM`s, creating different characters. The system prompts you create describes character of model in general, it is describing what model have to do and how."

    load_params = {
        "n_ctx": 8192,
        "n_batch": 512,
        "n_gpu_layers": 64,
        "use_mlock": True,
        "use_mmap": True,
    }

    inference_params = {
        "n_threads": 4,
        "n_predict": -1,
        "top_k": 40,
        "min_p": 0.05,
        "top_p": 0.95,
        "temp": 0.8,
        "repeat_penalty": 1.1,
        "input_prefix": "<|start_header_id|>user<|end_header_id|>\n\n",
        "input_suffix": "<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n",
        "antiprompt": [
        "<|start_header_id|>",
        "<|eot_id|>"
        ],
        "pre_prompt": "You are a LLM`s developer. You know them well and can provide answers on every question. You also can estimate users suggestions. You interested in creating different system prompts for LLM`s, creating different characters. The system prompts you create describes character of model in general, it is describing what model have to do and how.",
        "pre_prompt_suffix": "<|eot_id|>",
        "pre_prompt_prefix": "<|start_header_id|>system<|end_header_id|>\n\n",
        "seed": -1,
        "tfs_z": 1,
        "typical_p": 1,
        "repeat_last_n": 64,
        "frequency_penalty": 0,
        "presence_penalty": 0,
        "n_keep": 0,
        "logit_bias": {},
        "mirostat": 0,
        "mirostat_tau": 5,
        "mirostat_eta": 0.1,
        "memory_f16": True,
        "multiline_input": False,
        "penalize_nl": True
    }

    timeout_time = 25

    timeout_task = None

    def __init__(self):
        self.model_path = ""
        self.llm = None
        self.system_message = {
            "role": "system",
            "content": "You are an assistant who provides concise, contextually relevant, and accurate answers. If the user's message is unclear, ask for clarification instead of making assumptions. You never answer in JSON format."
        }
        self.messages = [self.system_message]

    def sent_json_debug(data):
        file_name = os.path.join(save_directory, "sent_json.json")
        with open(file_name, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=4)


    def create_app(self):
        app = FastAPI()
        handler = self

        @app.post("/send_model_path")
        async def send_model_path(model_path_client: FilePath):
            handler.model_path = model_path_client.path
            handler.llm = Llama(
                model_path=handler.model_path,
                verbose=False,
                n_ctx=8192,
                n_threads=0,
                n_gpu_layers=64,
                use_mmap = True,
                use_mlock = True,

            )
            response_text = f"Путь до загруженной модели: {model_path_client.path}"
            return {"response": response_text}

        @app.post("/send_message")
        async def send_message(message: TextMessage):
            user_input = message.content
            handler.messages.append({"role": "user", "content": user_input})

            response = handler.llm.create_chat_completion(messages=handler.messages)
            assistant_response = response['choices'][0]['message']['content']

            handler.messages.append({"role": "assistant", "content": assistant_response})

            #sent_json_debug("response": str(assistant_response))

            return {"response": str(assistant_response)}

        @app.post("/send_full_context")
        async def receive_json(request: Request):
            json_data = await request.json()

            file_name = os.path.join(save_directory, "received_data.json")
            with open(file_name, "w", encoding="utf-8") as f:
                json.dump(json_data, f, ensure_ascii=False, indent=4)

            handler.messages.clear()
            #handler.messages = None #должно полностью очищаться
            handler.messages.append(handler.system_message)

            #внимание прикол - json хранит в себе ключи с большой буквы у меня, а сервак - с маленькой
            #поэтому тут идет перевод с заглавных на строчные
            #На серваке должны всегда использоваться ключи с строчным 
            #это происходит потому, что я юзал с заглавной на фронте, а при сериализации они такими и остались,
            #а я про них нахуй забыл))

            for msg in json_data["Messages"]:
                handler.messages.append({

                    "role": msg["Role"],
                    "content": msg["Content"]

                })

            


            #return {"status": "Success", "file_saved_as": file_name}

        @app.post("/kill_server")
        async def kill_server():
            handler.llm = None
            handler.shutdown_server()

        @app.post("/ping")
        async def ping_from_client():
            await handler.ping_received()

        @app.post("/connected")
        async def start_timer():
            handler.timeout_task = asyncio.create_task(handler.timeout_check())

        return app

    def ping_received(self):
        self.timeout_task.cancel()
        self.timeout_task = asyncio.create_task(self.timeout_check())

    async def timeout_check(self):
        await asyncio.sleep(self.timeout_time)
        self.shutdown_server()

    def shutdown_server(self):
        import os
        import signal
        os.kill(os.getpid(), signal.SIGINT)




modelHandlerApp = ModelHandler()
app = modelHandlerApp.create_app()


# Запуск сервера
if __name__ == "__main__":
    import uvicorn
    
    uvicorn.run(app, host="127.0.0.1", port=8000)



'''
handler.llm = Llama(
                model_path=handler.model_path,
                verbose=False,
                n_ctx=8192,
                n_threads=0,
                n_gpu_layers=64,
                use_mmap = True,
                use_mlock = True,

            )

'''
