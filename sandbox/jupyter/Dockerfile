FROM egitim-python-sandbox:hafta-01

USER root
COPY requirements.txt /opt/notebook/requirements.txt
RUN pip install --no-cache-dir --requirement /opt/notebook/requirements.txt
COPY calistir.py /opt/notebook/calistir.py

ENV HOME=/tmp JUPYTER_RUNTIME_DIR=/tmp/jupyter-runtime IPYTHONDIR=/tmp/ipython
USER 10001:10001
CMD ["python", "/opt/notebook/calistir.py"]
