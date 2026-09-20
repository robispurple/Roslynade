llama-server `
    --model unsloth/gemma-4-12B-it-qat-GGUF/gemma-4-12B-it-qat-UD-Q4_K_XL.gguf `
    --mmproj unsloth/gemma-4-12B-it-qat-GGUF/mmproj-F16.gguf `
    --model-draft unsloth/gemma-4-12B-it-qat-GGUF/mtp-gemma-4-12B-it.gguf `
    --temp 1.0 `
    --top-p 0.95 `
    --top-k 64 `
    --alias "unsloth/gemma-4-12b-it-qat-GGUF" `
    --port 8001 `
    --chat-template-kwargs '{"enable_thinking":true}'