FROM python:3.13.17-slim-trixie

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTEST_DISABLE_PLUGIN_AUTOLOAD=1 \
    PIP_ROOT_USER_ACTION=ignore

COPY requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir --requirement /tmp/requirements.txt \
    && rm /tmp/requirements.txt \
    && groupadd --gid 10001 calistirici \
    && useradd --uid 10001 --gid 10001 --no-create-home --shell /usr/sbin/nologin calistirici

WORKDIR /workspace
USER 10001:10001

CMD ["python", "-m", "pytest", "-q"]
