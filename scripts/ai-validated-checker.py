#!/usr/bin/env python3

import os
import json
import google.generativeai as genai

def analyze_test_case(test_case_name):
    """
    Analyzes a single test case by comparing the input.cs and verified.txt files.
    """
    print(f"Analyzing test case: {test_case_name}...")

    base_path = "tests/FourSer.Tests/GeneratorTestCases"
    input_file_path = os.path.join(base_path, test_case_name, "input.cs")
    verified_file_path = os.path.join(base_path, test_case_name, f"{test_case_name}.RunGeneratorTest.verified.txt")

    try:
        with open(input_file_path, 'r', encoding='utf-8') as f:
            input_code = f.read()

        with open(verified_file_path, 'r', encoding='utf-8') as f:
            verified_code = f.read()

    except FileNotFoundError as e:
        return {
            "Test Case": test_case_name,
            "Status": "Error",
            "Message": f"File not found: {e.filename}"
        }
    except Exception as e:
        return {
            "Test Case": test_case_name,
            "Status": "Error",
            "Message": f"An error occurred during file reading: {e}"
        }

    # Now, call the Gemini API
    try:
        model = genai.GenerativeModel('gemini-1.5-flash')

        prompt = f"""
You are an expert C# static analysis tool specializing in the FourSer.Gen source generator. Your task is to analyze a C# source generator test case.

I will provide you with two files:
1.  `input.cs`: This file contains a partial class with attributes from the `FourSer.Contracts` library, like `[GenerateSerializer]` and `[SerializeCollection]`. This is the input to the source generator.
2.  `verified.txt`: This file contains the C# code that the source generator is expected to produce for the given `input.cs`.

**Your task:**
Analyze the `verified.txt` and determine if it is a correct and complete implementation of the serialization logic defined in `input.cs`.

**Key Serialization Rules:**
- A class with `[GenerateSerializer]` should have `ISerializable<T>` interface implemented.
- It should have static methods: `GetPacketSize`, `Serialize`, and `Deserialize`.
- A property with `[SerializeCollection]` should be handled in those methods. By default, the collection is prefixed with an `int` for the count.
- All public fields and properties of the class should be serialized and deserialized in the order they are defined.

**Input Files:**

**`input.cs`:**
```csharp
{input_code}
```

**`verified.txt`:**
```csharp
{verified_code}
```

**Analysis:**
Based on the rules, does the `verified.txt` correctly implement the `input.cs`?

**Output Format:**
Please provide your response as a single JSON object with two keys:
- `status`: A string, which can be one of "OK", "Warning", or "Error".
- `message`: A string, providing a detailed explanation of your findings. If the status is "OK", the message should be "No issues found."
        """

        response = model.generate_content(prompt)

        # The response text might be enclosed in ```json ... ```, so we need to clean it.
        cleaned_response = response.text.strip().lstrip("```json").rstrip("```").strip()

        analysis_result = json.loads(cleaned_response)

        return {
            "Test Case": test_case_name,
            "Status": analysis_result.get("status", "Error"),
            "Message": analysis_result.get("message", "Invalid JSON response from API.")
        }

    except Exception as e:
        return {
            "Test Case": test_case_name,
            "Status": "API Error",
            "Message": f"An error occurred while calling the Gemini API: {e}"
        }


def main():
    """
    Main function to run the validation checker.
    """
    # The API key is provided in the task description.
    # For security and best practices, we set it as an environment variable.
    api_key = "AIzaSyAti7mmuGl9JC3H8NteK1IyC_Y9y28RNTQ"
    if "GEMINI_API_KEY" not in os.environ:
        os.environ["GEMINI_API_KEY"] = api_key

    genai.configure(api_key=os.environ["GEMINI_API_KEY"])

    test_cases = [
        "Collection",
        "SimplePacket"
    ]

    results = []
    for test_case in test_cases:
        result = analyze_test_case(test_case)
        results.append(result)

    # Print results in a table
    print("\n--- Analysis Results ---")
    print(f"{'Test Case':<20} {'Status':<15} {'Message'}")
    print("-" * 60)
    for result in results:
        print(f"{result['Test Case']:<20} {result['Status']:<15} {result['Message']}")

if __name__ == "__main__":
    main()
