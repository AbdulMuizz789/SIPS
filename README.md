# SIPS
Major Project on Simulation of an Intelligent Parking guidance and information System made using Unity and SUMO.

## MODEL TRAINING
### REQUIREMENTS
- CUDA compliant GPU (for ML training)
- Python version >= 3.10.1, <= 3.10.12

### RUNNING THE ML TRAINING
1. Navigate to the MLAgent directory using `cd MLAgent`
2. Create a virtual environment using `python -m venv parking-ml-env`
3. Activate the virtual environment using `parking-ml-env\Scripts\activate`
4. Install the required dependencies using `pip install -r requirements.txt`
5. Open the training Unity Editor and run the ML training using `mlagents-learn config/trainer_config.yaml --run-id=parking-ml-run --force`
6. Click the Play button in the Unity Editor when prompted to start the training process.
